using CHRONIQ.Domain;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace CHRONIQ.Services;

/// <summary>
/// MailKit-based SMTP sender.
/// </summary>
public class MailKitSmtpMailSender : ISmtpMailSender
{
    public async Task SendAsync(
        NotificationSmtpSettings settings,
        string toEmail,
        string subject,
        string body,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentException.ThrowIfNullOrWhiteSpace(toEmail);
        ArgumentException.ThrowIfNullOrWhiteSpace(subject);

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(settings.FromDisplayName, settings.FromAddress));
        message.To.Add(MailboxAddress.Parse(toEmail));
        message.Subject = subject;
        message.Body = new TextPart("plain") { Text = body };

        using var client = new SmtpClient
        {
            Timeout = 10000
        };

        var socketOptions = settings.EnableSsl
            ? SecureSocketOptions.StartTls
            : SecureSocketOptions.None;

        await client.ConnectAsync(settings.Host, settings.Port, socketOptions, cancellationToken);

        if (!string.IsNullOrWhiteSpace(settings.Username))
        {
            await client.AuthenticateAsync(settings.Username, settings.Password, cancellationToken);
        }

        await client.SendAsync(message, cancellationToken);
        await client.DisconnectAsync(true, cancellationToken);
    }
}
