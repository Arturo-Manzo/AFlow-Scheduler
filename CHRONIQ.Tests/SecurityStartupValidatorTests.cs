using CHRONIQ.Api.Services;

namespace CHRONIQ.Tests;

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

    // ============================================
    // ValidateHttpsPipelineConsistency Tests
    // ============================================

    [Fact]
    public void ValidateHttpsPipelineConsistency_HttpsRedirectWithHttpOrigins_Throws()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            SecurityStartupValidator.ValidateHttpsPipelineConsistency(
                httpsRedirectionEnabled: true,
                corsOrigins: new[] { "http://192.168.1.65:4000" },
                isProduction: true));

        Assert.Contains("HttpsRedirection is enabled but CORS origins use HTTP", ex.Message);
        Assert.Contains("307 redirects", ex.Message);
    }

    [Fact]
    public void ValidateHttpsPipelineConsistency_HttpsRedirectWithHttpsOrigins_DoesNotThrow()
    {
        SecurityStartupValidator.ValidateHttpsPipelineConsistency(
            httpsRedirectionEnabled: true,
            corsOrigins: new[] { "https://192.168.1.65:4000" },
            isProduction: true);
    }

    [Fact]
    public void ValidateHttpsPipelineConsistency_NoRedirectWithHttpOrigins_DoesNotThrow()
    {
        SecurityStartupValidator.ValidateHttpsPipelineConsistency(
            httpsRedirectionEnabled: false,
            corsOrigins: new[] { "http://192.168.1.65:4000" },
            isProduction: true);
    }

    [Fact]
    public void ValidateHttpsPipelineConsistency_ProductionHttpsOriginsButRedirectDisabled_Throws()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            SecurityStartupValidator.ValidateHttpsPipelineConsistency(
                httpsRedirectionEnabled: false,
                corsOrigins: new[] { "https://app.company.com" },
                isProduction: true));

        Assert.Contains("CORS origins use HTTPS but HttpsRedirection is disabled", ex.Message);
    }

    [Fact]
    public void ValidateHttpsPipelineConsistency_NonProductionHttpsOriginsRedirectDisabled_DoesNotThrow()
    {
        SecurityStartupValidator.ValidateHttpsPipelineConsistency(
            httpsRedirectionEnabled: false,
            corsOrigins: new[] { "https://staging.company.com" },
            isProduction: false);
    }

    [Fact]
    public void ValidateHttpsPipelineConsistency_EmptyOrigins_DoesNotThrow()
    {
        SecurityStartupValidator.ValidateHttpsPipelineConsistency(
            httpsRedirectionEnabled: true,
            corsOrigins: Array.Empty<string>(),
            isProduction: false);
    }

    [Fact]
    public void ValidateHttpsPipelineConsistency_MixedOrigins_HttpsRedirectEnabled_Throws()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            SecurityStartupValidator.ValidateHttpsPipelineConsistency(
                httpsRedirectionEnabled: true,
                corsOrigins: new[] { "https://app.company.com", "http://internal.company.com:8080" },
                isProduction: true));

        Assert.Contains("http://internal.company.com:8080", ex.Message);
    }
}
