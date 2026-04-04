namespace AScheduler.Domain;

/// <summary>
/// Runtime SMTP notification settings loaded from persistent storage.
/// </summary>
public class NotificationSmtpSettings
{
    /// <summary>
    /// Whether SMTP notifications are enabled.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// SMTP server hostname.
    /// </summary>
    public string Host { get; set; } = "";

    /// <summary>
    /// SMTP server port.
    /// </summary>
    public int Port { get; set; } = 587;

    /// <summary>
    /// SMTP username.
    /// </summary>
    public string Username { get; set; } = "";

    /// <summary>
    /// SMTP password in plain text in-memory only.
    /// </summary>
    public string Password { get; set; } = "";

    /// <summary>
    /// Sender email address.
    /// </summary>
    public string FromAddress { get; set; } = "noreply@ascheduler.local";

    /// <summary>
    /// Sender display name.
    /// </summary>
    public string FromDisplayName { get; set; } = "AScheduler Notifications";

    /// <summary>
    /// Whether SSL/TLS is enabled.
    /// </summary>
    public bool EnableSsl { get; set; } = true;
}
