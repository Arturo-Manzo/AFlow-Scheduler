namespace AScheduler.Api.Services;

/// <summary>
/// Resolves JWT secret configuration using environment variable first,
/// with appsettings as a fallback for local/dev compatibility.
/// </summary>
public static class JwtSecretResolver
{
    /// <summary>
    /// Environment variable that overrides <c>Jwt:Secret</c> when present.
    /// </summary>
    public const string SecretEnvironmentVariableName = "ASCHEDULER_JWT_SECRET";

    /// <summary>
    /// Resolves JWT secret from environment variable, then from configuration fallback.
    /// </summary>
    /// <param name="configuration">Application configuration.</param>
    /// <returns>Resolved secret and whether appsettings fallback was used.</returns>
    /// <exception cref="ArgumentNullException">Thrown when configuration is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when no secret is available.</exception>
    /// <exception cref="ArgumentException">Thrown when secret is shorter than 32 characters.</exception>
    public static (string Secret, bool UsedAppSettingsFallback) Resolve(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var envSecret = Environment.GetEnvironmentVariable(SecretEnvironmentVariableName);
        if (!string.IsNullOrWhiteSpace(envSecret))
        {
            ValidateSecretLength(envSecret);
            return (envSecret, false);
        }

        var fallbackSecret = configuration["Jwt:Secret"];
        if (string.IsNullOrWhiteSpace(fallbackSecret))
        {
            throw new InvalidOperationException(
                $"Missing JWT secret. Set environment variable '{SecretEnvironmentVariableName}' or configure Jwt:Secret.");
        }

        ValidateSecretLength(fallbackSecret);
        return (fallbackSecret, true);
    }

    private static void ValidateSecretLength(string secret)
    {
        if (secret.Length < 32)
        {
            throw new ArgumentException("JWT Secret must be at least 32 characters long.");
        }
    }
}