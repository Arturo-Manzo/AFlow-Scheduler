using Microsoft.AspNetCore.Hosting;

namespace AScheduler.Api.Services;

/// <summary>
/// Validates startup security configuration for environment-specific behavior.
/// </summary>
public static class SecurityStartupValidator
{
    /// <summary>
    /// Validates JWT secret source policy.
    /// In production, appsettings fallback is forbidden.
    /// </summary>
    /// <param name="usedAppSettingsFallback">Whether Jwt:Secret fallback was used.</param>
    /// <param name="isProduction">Whether current environment is production.</param>
    /// <param name="secretEnvironmentVariableName">Required JWT environment variable name.</param>
    /// <exception cref="InvalidOperationException">Thrown when production uses fallback.</exception>
    public static void ValidateJwtSecretSource(
        bool usedAppSettingsFallback,
        bool isProduction,
        string secretEnvironmentVariableName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(secretEnvironmentVariableName);

        if (isProduction && usedAppSettingsFallback)
        {
            throw new InvalidOperationException(
                $"Production startup blocked: JWT fallback to appsettings is not allowed. Set '{secretEnvironmentVariableName}'.");
        }
    }

    /// <summary>
    /// Validates and normalizes configured CORS origins.
    /// </summary>
    /// <param name="configuredOrigins">Raw configured origins.</param>
    /// <param name="isDevelopment">Whether current environment is development.</param>
    /// <param name="isProduction">Whether current environment is production.</param>
    /// <returns>Normalized allowed origins.</returns>
    /// <exception cref="InvalidOperationException">Thrown when production has invalid CORS configuration.</exception>
    public static string[] ValidateAndNormalizeCorsOrigins(
        IEnumerable<string>? configuredOrigins,
        bool isDevelopment,
        bool isProduction)
    {
        var normalized = (configuredOrigins ?? Array.Empty<string>())
            .Where(origin => !string.IsNullOrWhiteSpace(origin))
            .Select(origin => origin.Trim())
            .ToArray();

        if (normalized.Any(origin => origin.Contains('*')))
        {
            throw new InvalidOperationException("Cors:AllowedOrigins cannot contain wildcard entries.");
        }

        var validOrigins = normalized
            .Select(NormalizeOrigin)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (isProduction && validOrigins.Length == 0)
        {
            throw new InvalidOperationException(
                "Production startup blocked: Cors:AllowedOrigins must contain at least one explicit frontend origin.");
        }

        if (isProduction && validOrigins.Any(IsLocalhostOrigin))
        {
            throw new InvalidOperationException(
                "Production startup blocked: localhost origins are not allowed in Cors:AllowedOrigins.");
        }

        if (!isDevelopment && !isProduction && validOrigins.Length == 0)
        {
            throw new InvalidOperationException(
                "Non-development startup blocked: Cors:AllowedOrigins must be configured explicitly.");
        }

        return validOrigins;
    }

    private static string NormalizeOrigin(string origin)
    {
        if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri))
        {
            throw new InvalidOperationException($"Invalid CORS origin '{origin}': expected absolute URL.");
        }

        if (uri.Scheme is not ("http" or "https"))
        {
            throw new InvalidOperationException($"Invalid CORS origin '{origin}': only http/https schemes are supported.");
        }

        if (!string.IsNullOrEmpty(uri.AbsolutePath) && uri.AbsolutePath != "/")
        {
            throw new InvalidOperationException($"Invalid CORS origin '{origin}': path segment is not allowed.");
        }

        if (!string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
        {
            throw new InvalidOperationException($"Invalid CORS origin '{origin}': query/fragment is not allowed.");
        }

        return $"{uri.Scheme}://{uri.Authority}";
    }

    private static bool IsLocalhostOrigin(string origin)
    {
        if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri))
        {
            return false;
        }

        return uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
            || uri.Host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase)
            || uri.Host.Equals("::1", StringComparison.OrdinalIgnoreCase);
    }
}
