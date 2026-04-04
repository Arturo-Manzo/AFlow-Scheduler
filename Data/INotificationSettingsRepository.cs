using AScheduler.Domain;

namespace AScheduler.Data;

/// <summary>
/// Repository for reading and updating persistent notification settings.
/// </summary>
public interface INotificationSettingsRepository
{
    /// <summary>
    /// Gets the effective SMTP settings. Falls back to appsettings defaults when DB settings are unavailable.
    /// </summary>
    Task<NotificationSmtpSettings> GetEffectiveSmtpSettingsAsync();

    /// <summary>
    /// Persists SMTP settings. When <paramref name="overwritePassword"/> is false, existing password is preserved.
    /// </summary>
    Task<NotificationSmtpSettings> UpsertSmtpSettingsAsync(
        NotificationSmtpSettings settings,
        int? updatedByUserId,
        bool overwritePassword);
}
