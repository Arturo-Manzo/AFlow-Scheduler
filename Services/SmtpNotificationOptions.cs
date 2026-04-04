using System.ComponentModel.DataAnnotations;

namespace AScheduler.Services
{
    /// <summary>
    /// Configuration options for SMTP-based task failure notifications.
    /// These settings control whether and how task failure email alerts are sent.
    /// </summary>
    public class SmtpNotificationOptions
    {
        /// <summary>
        /// Enables or disables SMTP notification sending.
        /// When false, no notifications are sent even if box emails are configured.
        /// Default: false (not enabled in production until user configures it).
        /// </summary>
        public bool Enabled { get; set; } = false;

        /// <summary>
        /// SMTP server hostname (e.g., "smtp.gmail.com" or "mail.company.com").
        /// Required if notifications are enabled.
        /// </summary>
        [Required(AllowEmptyStrings = false)]
        public string Host { get; set; } = "";

        /// <summary>
        /// SMTP server port (typically 25, 587, or 465).
        /// Default: 587 (TLS submission port).
        /// </summary>
        [Range(1, 65535)]
        public int Port { get; set; } = 587;

        /// <summary>
        /// SMTP authentication username. Leave empty if server does not require authentication.
        /// </summary>
        public string Username { get; set; } = "";

        /// <summary>
        /// SMTP authentication password. Required if Username is provided.
        /// </summary>
        public string Password { get; set; } = "";

        /// <summary>
        /// Email address to use as sender (From field).
        /// Should be an address owned or authorized by the SMTP server.
        /// Default: "noreply@ascheduler.local"
        /// </summary>
        public string FromAddress { get; set; } = "noreply@ascheduler.local";

        /// <summary>
        /// Display name for the From field (e.g., "AScheduler Notifications").
        /// </summary>
        public string FromDisplayName { get; set; } = "AScheduler Notifications";

        /// <summary>
        /// Whether to use SSL/TLS when connecting to the SMTP server.
        /// Default: true (recommended for security).
        /// </summary>
        public bool EnableSsl { get; set; } = true;
    }
}
