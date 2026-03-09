using FirebaseAdmin;
using FirebaseAdmin.Auth;
using Google.Apis.Auth;
using Google.Apis.Auth.OAuth2;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using System.Net.Http.Json;
using System.Text.Json;

namespace ScaleApiPoc.Authentication;

public sealed class FirebaseAuthOptions
{
    public string? ProjectId { get; set; }
    public string? CredentialsPath { get; set; }
    public string? GoogleClientId { get; set; }
    public string? WebApiKey { get; set; }
}

public interface IFirebaseTokenVerifier
{
    Task<FirebaseToken> VerifyIdTokenAsync(string idToken, CancellationToken cancellationToken = default);
}

public interface IFirebaseAccountService
{
    Task<FirebaseAccountResult> CreateWithEmailPasswordAsync(
        string email,
        string password,
        string? displayName,
        CancellationToken cancellationToken = default);

    Task<FirebaseAccountResult> CreateOrSignInWithGoogleAsync(
        string googleIdToken,
        CancellationToken cancellationToken = default);

    Task<FirebaseSignInResult> SignInWithEmailPasswordAsync(
        string email,
        string password,
        CancellationToken cancellationToken = default);

    Task<FirebaseSignInResult> SignInWithGoogleAsync(
        string googleIdToken,
        CancellationToken cancellationToken = default);
}

public sealed class FirebaseAccountResult
{
    public required string Uid { get; init; }
    public required string Email { get; init; }
    public string? DisplayName { get; init; }
    public required string CustomToken { get; init; }
    public required bool IsNewUser { get; init; }
}

public sealed class FirebaseSignInResult
{
    public required string Uid { get; init; }
    public required string Email { get; init; }
    public string? DisplayName { get; init; }
    public required string IdToken { get; init; }
    public required string RefreshToken { get; init; }
    public required long ExpiresInSeconds { get; init; }
    public bool IsNewUser { get; init; }
}

public sealed class FirebaseIdentitySignInException : Exception
{
    public string ErrorCode { get; }

    public FirebaseIdentitySignInException(string errorCode)
        : base(errorCode)
    {
        ErrorCode = errorCode;
    }
}

internal sealed class FirebaseTokenVerifier : IFirebaseTokenVerifier
{
    private readonly FirebaseAuth _firebaseAuth;

    public FirebaseTokenVerifier(FirebaseAuth firebaseAuth)
    {
        _firebaseAuth = firebaseAuth;
    }

    public Task<FirebaseToken> VerifyIdTokenAsync(string idToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(idToken))
        {
            throw new ArgumentException("Firebase ID token is required.", nameof(idToken));
        }

        return _firebaseAuth.VerifyIdTokenAsync(idToken, cancellationToken);
    }
}

internal sealed class FirebaseAccountService : IFirebaseAccountService
{
    private readonly FirebaseAuth _firebaseAuth;
    private readonly FirebaseAuthOptions _options;
    private readonly IHttpClientFactory _httpClientFactory;

    public FirebaseAccountService(
        FirebaseAuth firebaseAuth,
        IOptions<FirebaseAuthOptions> options,
        IHttpClientFactory httpClientFactory)
    {
        _firebaseAuth = firebaseAuth;
        _options = options.Value;
        _httpClientFactory = httpClientFactory;
    }

    public async Task<FirebaseAccountResult> CreateWithEmailPasswordAsync(
        string email,
        string password,
        string? displayName,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            throw new ArgumentException("Email is required.", nameof(email));
        }

        if (string.IsNullOrWhiteSpace(password))
        {
            throw new ArgumentException("Password is required.", nameof(password));
        }

        var createdUser = await _firebaseAuth.CreateUserAsync(new UserRecordArgs
        {
            Email = email.Trim(),
            Password = password,
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? null : displayName.Trim(),
            EmailVerified = false
        }, cancellationToken);

        var customToken = await _firebaseAuth.CreateCustomTokenAsync(createdUser.Uid, cancellationToken);
        return new FirebaseAccountResult
        {
            Uid = createdUser.Uid,
            Email = createdUser.Email,
            DisplayName = createdUser.DisplayName,
            CustomToken = customToken,
            IsNewUser = true
        };
    }

    public async Task<FirebaseAccountResult> CreateOrSignInWithGoogleAsync(
        string googleIdToken,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(googleIdToken))
        {
            throw new ArgumentException("Google ID token is required.", nameof(googleIdToken));
        }

        var validationSettings = new GoogleJsonWebSignature.ValidationSettings();
        if (!string.IsNullOrWhiteSpace(_options.GoogleClientId))
        {
            validationSettings.Audience = new[] { _options.GoogleClientId };
        }

        var payload = await GoogleJsonWebSignature.ValidateAsync(googleIdToken, validationSettings);
        if (string.IsNullOrWhiteSpace(payload.Email))
        {
            throw new InvalidOperationException("Google token does not contain an email.");
        }

        var email = payload.Email.Trim();
        UserRecord user;
        var isNewUser = false;
        try
        {
            user = await _firebaseAuth.GetUserByEmailAsync(email, cancellationToken);
        }
        catch (FirebaseAuthException ex) when (ex.AuthErrorCode == AuthErrorCode.UserNotFound)
        {
            user = await _firebaseAuth.CreateUserAsync(new UserRecordArgs
            {
                Email = email,
                EmailVerified = payload.EmailVerified,
                DisplayName = payload.Name,
                PhotoUrl = payload.Picture
            }, cancellationToken);
            isNewUser = true;
        }

        var customToken = await _firebaseAuth.CreateCustomTokenAsync(user.Uid, cancellationToken);
        return new FirebaseAccountResult
        {
            Uid = user.Uid,
            Email = user.Email,
            DisplayName = user.DisplayName,
            CustomToken = customToken,
            IsNewUser = isNewUser
        };
    }

    public Task<FirebaseSignInResult> SignInWithEmailPasswordAsync(
        string email,
        string password,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            throw new ArgumentException("Email is required.", nameof(email));
        }

        if (string.IsNullOrWhiteSpace(password))
        {
            throw new ArgumentException("Password is required.", nameof(password));
        }

        return SignInWithIdentityToolkitAsync(
            "accounts:signInWithPassword",
            new
            {
                email = email.Trim(),
                password,
                returnSecureToken = true
            },
            cancellationToken);
    }

    public Task<FirebaseSignInResult> SignInWithGoogleAsync(
        string googleIdToken,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(googleIdToken))
        {
            throw new ArgumentException("Google ID token is required.", nameof(googleIdToken));
        }

        return SignInWithIdentityToolkitAsync(
            "accounts:signInWithIdp",
            new
            {
                postBody = $"id_token={Uri.EscapeDataString(googleIdToken)}&providerId=google.com",
                requestUri = "http://localhost",
                returnSecureToken = true,
                returnIdpCredential = true
            },
            cancellationToken);
    }

    private async Task<FirebaseSignInResult> SignInWithIdentityToolkitAsync(
        string endpoint,
        object payload,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.WebApiKey))
        {
            throw new InvalidOperationException("Firebase WebApiKey is required for sign-in.");
        }

        var url = $"https://identitytoolkit.googleapis.com/v1/{endpoint}?key={_options.WebApiKey}";
        var httpClient = _httpClientFactory.CreateClient();

        using var response = await httpClient.PostAsJsonAsync(url, payload, cancellationToken);
        var json = await response.Content.ReadAsStringAsync(cancellationToken);

        using var document = JsonDocument.Parse(json);
        if (!response.IsSuccessStatusCode)
        {
            var message = document.RootElement
                .GetProperty("error")
                .GetProperty("message")
                .GetString() ?? "UNKNOWN";
            throw new FirebaseIdentitySignInException(message);
        }

        var root = document.RootElement;
        var uid = root.GetProperty("localId").GetString() ?? string.Empty;
        var email = root.TryGetProperty("email", out var emailProp) ? emailProp.GetString() ?? string.Empty : string.Empty;
        var displayName = root.TryGetProperty("displayName", out var displayNameProp) ? displayNameProp.GetString() : null;
        var idToken = root.GetProperty("idToken").GetString() ?? string.Empty;
        var refreshToken = root.GetProperty("refreshToken").GetString() ?? string.Empty;
        var expiresInRaw = root.GetProperty("expiresIn").GetString() ?? "0";
        _ = long.TryParse(expiresInRaw, out var expiresInSeconds);
        var isNewUser = root.TryGetProperty("isNewUser", out var isNewUserProp) && isNewUserProp.GetBoolean();

        return new FirebaseSignInResult
        {
            Uid = uid,
            Email = email,
            DisplayName = displayName,
            IdToken = idToken,
            RefreshToken = refreshToken,
            ExpiresInSeconds = expiresInSeconds,
            IsNewUser = isNewUser
        };
    }
}

internal sealed class FirebaseAppProvider : IDisposable
{
    public FirebaseApp App { get; }

    public FirebaseAppProvider(IOptions<FirebaseAuthOptions> options)
    {
        var configuredOptions = options.Value;

        var appOptions = new AppOptions();
        if (!string.IsNullOrWhiteSpace(configuredOptions.ProjectId))
        {
            appOptions.ProjectId = configuredOptions.ProjectId;
        }

        if (!string.IsNullOrWhiteSpace(configuredOptions.CredentialsPath))
        {
            appOptions.Credential = GoogleCredential.FromFile(configuredOptions.CredentialsPath);
        }
        else
        {
            appOptions.Credential = GoogleCredential.GetApplicationDefault();
        }

        App = FirebaseApp.Create(appOptions, "ScaleApiPoc.Authentication");
    }

    public void Dispose()
    {
        App.Delete();
    }
}

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddScaleApiPocAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<FirebaseAuthOptions>(configuration.GetSection("Firebase"));

        services.AddHttpClient();
        services.AddSingleton<FirebaseAppProvider>();
        services.AddSingleton(sp =>
        {
            var appProvider = sp.GetRequiredService<FirebaseAppProvider>();
            return FirebaseAuth.GetAuth(appProvider.App);
        });
        services.AddScoped<IFirebaseTokenVerifier, FirebaseTokenVerifier>();
        services.AddScoped<IFirebaseAccountService, FirebaseAccountService>();

        return services;
    }
}
