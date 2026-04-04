using System.Data;
using AScheduler.Domain;
using AScheduler.Services;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;

namespace AScheduler.Data;

/// <summary>
/// Dapper repository for SMTP notification settings.
/// </summary>
public class NotificationSettingsRepository : INotificationSettingsRepository
{
    private readonly string _connectionString;
    private readonly INotificationSecretProtector _secretProtector;
    private readonly SmtpNotificationOptions _fallbackOptions;
    private readonly ILogger<NotificationSettingsRepository> _logger;

    private sealed class NotificationSmtpSettingsRecord
    {
        public bool Enabled { get; set; }
        public string? Host { get; set; }
        public int Port { get; set; }
        public string? Username { get; set; }
        public string? EncryptedPassword { get; set; }
        public string? FromAddress { get; set; }
        public string? FromDisplayName { get; set; }
        public bool EnableSsl { get; set; }
    }

    public NotificationSettingsRepository(
        IConfiguration configuration,
        INotificationSecretProtector secretProtector,
        IOptions<SmtpNotificationOptions> fallbackOptions,
        ILogger<NotificationSettingsRepository> logger)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(secretProtector);
        ArgumentNullException.ThrowIfNull(fallbackOptions);
        ArgumentNullException.ThrowIfNull(logger);

        _connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("Missing 'Default' connection string in configuration.");
        _secretProtector = secretProtector;
        _fallbackOptions = fallbackOptions.Value;
        _logger = logger;
    }

    public async Task<NotificationSmtpSettings> GetEffectiveSmtpSettingsAsync()
    {
        try
        {
            using IDbConnection connection = new SqlConnection(_connectionString);
            const string sql = @"
                SELECT TOP 1
                    Enabled,
                    Host,
                    Port,
                    Username,
                    EncryptedPassword,
                    FromAddress,
                    FromDisplayName,
                    EnableSsl
                FROM NotificationSmtpSettings
                WHERE SettingsId = 1";

            var record = await connection.QueryFirstOrDefaultAsync<NotificationSmtpSettingsRecord>(sql);
            if (record == null)
            {
                return BuildFromFallback();
            }

            return new NotificationSmtpSettings
            {
                Enabled = record.Enabled,
                Host = record.Host ?? string.Empty,
                Port = record.Port,
                Username = record.Username ?? string.Empty,
                Password = _secretProtector.Unprotect(record.EncryptedPassword ?? string.Empty),
                FromAddress = record.FromAddress ?? "noreply@ascheduler.local",
                FromDisplayName = record.FromDisplayName ?? "AScheduler Notifications",
                EnableSsl = record.EnableSsl
            };
        }
        catch (SqlException ex) when (ex.Number == 208)
        {
            _logger.LogWarning(
                ex,
                "NotificationSmtpSettings table not found. Falling back to appsettings SMTP options.");
            return BuildFromFallback();
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to load SMTP settings from database. Falling back to appsettings SMTP options.");
            return BuildFromFallback();
        }
    }

    public async Task<NotificationSmtpSettings> UpsertSmtpSettingsAsync(
        NotificationSmtpSettings settings,
        int? updatedByUserId,
        bool overwritePassword)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var effectivePassword = settings.Password;

        using IDbConnection connection = new SqlConnection(_connectionString);

        if (!overwritePassword)
        {
            const string currentPasswordSql = @"
                SELECT TOP 1 EncryptedPassword
                FROM NotificationSmtpSettings
                WHERE SettingsId = 1";

            var existingEncryptedPassword = await connection.QueryFirstOrDefaultAsync<string>(currentPasswordSql);
            if (!string.IsNullOrEmpty(existingEncryptedPassword))
            {
                effectivePassword = _secretProtector.Unprotect(existingEncryptedPassword);
            }
        }

        var protectedPassword = _secretProtector.Protect(effectivePassword ?? string.Empty);

        const string mergeSql = @"
            MERGE NotificationSmtpSettings AS target
            USING (SELECT 1 AS SettingsId) AS source
            ON target.SettingsId = source.SettingsId
            WHEN MATCHED THEN
                UPDATE SET
                    Enabled = @Enabled,
                    Host = @Host,
                    Port = @Port,
                    Username = @Username,
                    EncryptedPassword = @EncryptedPassword,
                    FromAddress = @FromAddress,
                    FromDisplayName = @FromDisplayName,
                    EnableSsl = @EnableSsl,
                    UpdatedByUserId = @UpdatedByUserId,
                    UpdatedAtUtc = SYSUTCDATETIME()
            WHEN NOT MATCHED THEN
                INSERT
                (
                    SettingsId,
                    Enabled,
                    Host,
                    Port,
                    Username,
                    EncryptedPassword,
                    FromAddress,
                    FromDisplayName,
                    EnableSsl,
                    UpdatedByUserId,
                    CreatedAtUtc,
                    UpdatedAtUtc
                )
                VALUES
                (
                    1,
                    @Enabled,
                    @Host,
                    @Port,
                    @Username,
                    @EncryptedPassword,
                    @FromAddress,
                    @FromDisplayName,
                    @EnableSsl,
                    @UpdatedByUserId,
                    SYSUTCDATETIME(),
                    SYSUTCDATETIME()
                );";

        await connection.ExecuteAsync(mergeSql, new
        {
            settings.Enabled,
            Host = string.IsNullOrWhiteSpace(settings.Host) ? null : settings.Host.Trim(),
            settings.Port,
            Username = string.IsNullOrWhiteSpace(settings.Username) ? null : settings.Username.Trim(),
            EncryptedPassword = protectedPassword,
            FromAddress = string.IsNullOrWhiteSpace(settings.FromAddress) ? null : settings.FromAddress.Trim(),
            FromDisplayName = string.IsNullOrWhiteSpace(settings.FromDisplayName) ? "AScheduler Notifications" : settings.FromDisplayName.Trim(),
            settings.EnableSsl,
            UpdatedByUserId = updatedByUserId
        });

        return await GetEffectiveSmtpSettingsAsync();
    }

    private NotificationSmtpSettings BuildFromFallback()
    {
        return new NotificationSmtpSettings
        {
            Enabled = _fallbackOptions.Enabled,
            Host = _fallbackOptions.Host,
            Port = _fallbackOptions.Port,
            Username = _fallbackOptions.Username,
            Password = _fallbackOptions.Password,
            FromAddress = _fallbackOptions.FromAddress,
            FromDisplayName = _fallbackOptions.FromDisplayName,
            EnableSsl = _fallbackOptions.EnableSsl
        };
    }
}
