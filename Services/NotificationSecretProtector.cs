using Microsoft.AspNetCore.DataProtection;

namespace CHRONIQ.Services;

/// <summary>
/// DataProtection-based secret protector for notification credentials.
/// </summary>
public class NotificationSecretProtector : INotificationSecretProtector
{
    private readonly IDataProtector _protector;

    public NotificationSecretProtector(IDataProtectionProvider dataProtectionProvider)
    {
        ArgumentNullException.ThrowIfNull(dataProtectionProvider);
        _protector = dataProtectionProvider.CreateProtector("CHRONIQ.NotificationSettings.SmtpPassword.v1");
    }

    public string Protect(string plainText)
    {
        if (string.IsNullOrEmpty(plainText))
        {
            return string.Empty;
        }

        return _protector.Protect(plainText);
    }

    public string Unprotect(string protectedText)
    {
        if (string.IsNullOrEmpty(protectedText))
        {
            return string.Empty;
        }

        return _protector.Unprotect(protectedText);
    }
}
