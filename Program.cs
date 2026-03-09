using Microsoft.EntityFrameworkCore;
using Npgsql;
using ScaleApiPoc.Authentication;
using ScaleApiPoc.Data;

var builder = WebApplication.CreateBuilder(args);

if (builder.Environment.IsDevelopment() && string.IsNullOrWhiteSpace(builder.Configuration["Firebase:CredentialsJson"]))
{
    var firebaseDirectory = Path.Combine(
        builder.Environment.ContentRootPath,
        "ScaleApiPoc.Authentication",
        "firebase");
    if (Directory.Exists(firebaseDirectory))
    {
        var credentialsFilePath = Directory
            .GetFiles(firebaseDirectory, "*-adminsdk-*.json")
            .FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(credentialsFilePath))
        {
            builder.Configuration["Firebase:CredentialsJson"] = File.ReadAllText(credentialsFilePath);
        }
    }
}

// Resolve connection string: Neon (and others) often give a postgresql:// URI; Npgsql needs key=value format.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? "";
if (connectionString.TrimStart().StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase)
    || connectionString.TrimStart().StartsWith("postgres://", StringComparison.OrdinalIgnoreCase))
{
    var uri = new Uri(connectionString);
    var userInfo = uri.UserInfo.Split(':', 2);
    var npgsqlBuilder = new NpgsqlConnectionStringBuilder
    {
        Host = uri.Host,
        Port = uri.Port > 0 ? uri.Port : 5432,
        Database = uri.AbsolutePath.TrimStart('/'),
        Username = userInfo.Length > 0 ? Uri.UnescapeDataString(userInfo[0]) : null,
        Password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : null,
        SslMode = SslMode.Require
    };
    var query = uri.Query.TrimStart('?');
    if (!string.IsNullOrEmpty(query))
    {
        foreach (var pair in query.Split('&'))
        {
            var kv = pair.Split('=', 2, StringSplitOptions.None);
            if (kv.Length == 2 && kv[0].Equals("sslmode", StringComparison.OrdinalIgnoreCase))
            {
                npgsqlBuilder.SslMode = kv[1].Trim().ToLowerInvariant() switch
                {
                    "disable" => SslMode.Disable,
                    "prefer" => SslMode.Prefer,
                    "require" => SslMode.Require,
                    "verify-ca" => SslMode.VerifyCA,
                    "verify-full" => SslMode.VerifyFull,
                    _ => SslMode.Require
                };
                break;
            }
        }
    }
    connectionString = npgsqlBuilder.ConnectionString;
}

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddScaleApiPocAuthentication(builder.Configuration);
builder.Services.AddDbContext<DataContext>(options =>
    options.UseNpgsql(connectionString));
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins("http://localhost:5173")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});
//builder.WebHost.ConfigureKestrel(serverOptions =>
//{
//    serverOptions.ListenAnyIP(8080);
//});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/openapi/v1.json", "v1");
    });
}

app.UseCors();

app.UseHttpsRedirection();

app.MapControllers();

var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapGet("/weatherforecast", () =>
{
    var forecast =  Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();
    return forecast;
})
.WithName("GetWeatherForecast");

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
