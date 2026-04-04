using AScheduler.Api.Services;

namespace AScheduler.Tests;

public class SecurityStartupValidatorTests
{
    [Fact]
    public void ValidateJwtSecretSource_ProductionWithFallback_Throws()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            SecurityStartupValidator.ValidateJwtSecretSource(
                usedAppSettingsFallback: true,
                isProduction: true,
                secretEnvironmentVariableName: JwtSecretResolver.SecretEnvironmentVariableName));

        Assert.Contains("Production startup blocked", ex.Message);
    }

    [Fact]
    public void ValidateJwtSecretSource_ProductionWithEnv_DoesNotThrow()
    {
        SecurityStartupValidator.ValidateJwtSecretSource(
            usedAppSettingsFallback: false,
            isProduction: true,
            secretEnvironmentVariableName: JwtSecretResolver.SecretEnvironmentVariableName);
    }

    [Fact]
    public void ValidateAndNormalizeCorsOrigins_ProductionWithoutOrigins_Throws()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            SecurityStartupValidator.ValidateAndNormalizeCorsOrigins(
                configuredOrigins: Array.Empty<string>(),
                isDevelopment: false,
                isProduction: true));

        Assert.Contains("Cors:AllowedOrigins", ex.Message);
    }

    [Fact]
    public void ValidateAndNormalizeCorsOrigins_DevelopmentWithoutOrigins_ReturnsEmpty()
    {
        var result = SecurityStartupValidator.ValidateAndNormalizeCorsOrigins(
            configuredOrigins: Array.Empty<string>(),
            isDevelopment: true,
            isProduction: false);

        Assert.Empty(result);
    }

    [Fact]
    public void ValidateAndNormalizeCorsOrigins_NormalizesAndDeduplicates()
    {
        var result = SecurityStartupValidator.ValidateAndNormalizeCorsOrigins(
            configuredOrigins: new[]
            {
                " https://app.company.com/ ",
                "https://app.company.com",
                "http://ops.company.com:8080"
            },
            isDevelopment: false,
            isProduction: true);

        Assert.Equal(2, result.Length);
        Assert.Contains("https://app.company.com", result);
        Assert.Contains("http://ops.company.com:8080", result);
    }

    [Fact]
    public void ValidateAndNormalizeCorsOrigins_ProductionWithLocalhost_Throws()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            SecurityStartupValidator.ValidateAndNormalizeCorsOrigins(
                configuredOrigins: new[] { "https://localhost:4200" },
                isDevelopment: false,
                isProduction: true));

        Assert.Contains("localhost origins are not allowed", ex.Message);
    }

    [Fact]
    public void ValidateAndNormalizeCorsOrigins_WithWildcard_Throws()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            SecurityStartupValidator.ValidateAndNormalizeCorsOrigins(
                configuredOrigins: new[] { "https://*.company.com" },
                isDevelopment: false,
                isProduction: true));

        Assert.Contains("wildcard", ex.Message);
    }
}
