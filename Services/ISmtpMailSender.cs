using CHRONIQ.Domain;

namespace CHRONIQ.Services;

/// <summary>
/// Sends plain text emails using SMTP settings.
/// </summary>
public interface ISmtpMailSender
{
    /// <summary>
    /// Sends a plain text email.
    /// </summary>
    Task SendAsync(
        NotificationSmtpSettings settings,
        string toEmail,
        string subject,
        string body,
        CancellationToken cancellationToken);
}
