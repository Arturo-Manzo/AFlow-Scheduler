using System.Security.Cryptography;

namespace AScheduler.ConfigWizard.Wpf.Services;

/// <summary>
/// Generates secure secrets used by authentication settings.
/// </summary>
public static class SecretGenerator
{
    public static string GenerateBase64Secret(int bytes = 64)
    {
        var buffer = new byte[bytes];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(buffer);
        return Convert.ToBase64String(buffer);
    }
}
