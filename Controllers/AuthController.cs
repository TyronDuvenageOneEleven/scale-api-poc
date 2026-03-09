using FirebaseAdmin.Auth;
using Google.Apis.Auth;
using Microsoft.AspNetCore.Mvc;
using ScaleApiPoc.Authentication;
using System.ComponentModel.DataAnnotations;

namespace scale_api_poc.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IFirebaseAccountService _firebaseAccountService;

    public AuthController(IFirebaseAccountService firebaseAccountService)
    {
        _firebaseAccountService = firebaseAccountService;
    }

    [HttpPost("register/email-password")]
    [ProducesResponseType(typeof(AuthAccountResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RegisterWithEmailPassword(
        [FromBody] EmailPasswordRegistrationRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _firebaseAccountService.CreateWithEmailPasswordAsync(
                request.Email,
                request.Password,
                request.DisplayName,
                cancellationToken);

            return Ok(AuthAccountResponse.FromResult(result));
        }
        catch (FirebaseAuthException ex) when (ex.AuthErrorCode == AuthErrorCode.EmailAlreadyExists)
        {
            return Conflict(new { message = "An account with this email already exists." });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("register/google")]
    [ProducesResponseType(typeof(AuthAccountResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RegisterWithGoogle(
        [FromBody] GoogleRegistrationRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _firebaseAccountService.CreateOrSignInWithGoogleAsync(
                request.GoogleIdToken,
                cancellationToken);

            return Ok(AuthAccountResponse.FromResult(result));
        }
        catch (InvalidJwtException ex)
        {
            return BadRequest(new { message = $"Invalid Google token: {ex.Message}" });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("signin/email-password")]
    [ProducesResponseType(typeof(AuthSignInResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> SignInWithEmailPassword(
        [FromBody] EmailPasswordSignInRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _firebaseAccountService.SignInWithEmailPasswordAsync(
                request.Email,
                request.Password,
                cancellationToken);

            return Ok(AuthSignInResponse.FromResult(result));
        }
        catch (FirebaseIdentitySignInException ex) when (ex.ErrorCode is "EMAIL_NOT_FOUND" or "INVALID_PASSWORD" or "USER_DISABLED")
        {
            return Unauthorized(new { message = "Invalid email or password." });
        }
        catch (FirebaseIdentitySignInException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("signin/google")]
    [ProducesResponseType(typeof(AuthSignInResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> SignInWithGoogle(
        [FromBody] GoogleSignInRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _firebaseAccountService.SignInWithGoogleAsync(
                request.GoogleIdToken,
                cancellationToken);

            return Ok(AuthSignInResponse.FromResult(result));
        }
        catch (FirebaseIdentitySignInException ex) when (ex.ErrorCode is "INVALID_IDP_RESPONSE" or "INVALID_ID_TOKEN")
        {
            return Unauthorized(new { message = "Invalid Google token." });
        }
        catch (FirebaseIdentitySignInException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}

public sealed class EmailPasswordRegistrationRequest
{
    [Required]
    [EmailAddress]
    public string Email { get; init; } = string.Empty;

    [Required]
    [MinLength(6)]
    public string Password { get; init; } = string.Empty;

    public string? DisplayName { get; init; }
}

public sealed class GoogleRegistrationRequest
{
    [Required]
    public string GoogleIdToken { get; init; } = string.Empty;
}

public sealed class EmailPasswordSignInRequest
{
    [Required]
    [EmailAddress]
    public string Email { get; init; } = string.Empty;

    [Required]
    public string Password { get; init; } = string.Empty;
}

public sealed class GoogleSignInRequest
{
    [Required]
    public string GoogleIdToken { get; init; } = string.Empty;
}

public sealed class AuthAccountResponse
{
    public required string Uid { get; init; }
    public required string Email { get; init; }
    public string? DisplayName { get; init; }
    public required string CustomToken { get; init; }
    public required bool IsNewUser { get; init; }

    public static AuthAccountResponse FromResult(FirebaseAccountResult result)
    {
        return new AuthAccountResponse
        {
            Uid = result.Uid,
            Email = result.Email,
            DisplayName = result.DisplayName,
            CustomToken = result.CustomToken,
            IsNewUser = result.IsNewUser
        };
    }
}

public sealed class AuthSignInResponse
{
    public required string Uid { get; init; }
    public required string Email { get; init; }
    public string? DisplayName { get; init; }
    public required string IdToken { get; init; }
    public required string RefreshToken { get; init; }
    public required long ExpiresInSeconds { get; init; }
    public bool IsNewUser { get; init; }

    public static AuthSignInResponse FromResult(FirebaseSignInResult result)
    {
        return new AuthSignInResponse
        {
            Uid = result.Uid,
            Email = result.Email,
            DisplayName = result.DisplayName,
            IdToken = result.IdToken,
            RefreshToken = result.RefreshToken,
            ExpiresInSeconds = result.ExpiresInSeconds,
            IsNewUser = result.IsNewUser
        };
    }
}
