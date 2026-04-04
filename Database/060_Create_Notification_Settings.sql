-- ============================================
-- 060_Create_Notification_Settings.sql
-- Persistent SMTP configuration singleton.
-- ============================================

CREATE TABLE dbo.NotificationSmtpSettings
(
    SettingsId INT NOT NULL
        CONSTRAINT PK_NotificationSmtpSettings PRIMARY KEY
        CONSTRAINT CK_NotificationSmtpSettings_Singleton CHECK (SettingsId = 1),
    Enabled BIT NOT NULL CONSTRAINT DF_NotificationSmtpSettings_Enabled DEFAULT (0),
    Host NVARCHAR(255) NULL,
    Port INT NOT NULL
        CONSTRAINT DF_NotificationSmtpSettings_Port DEFAULT (587)
        CONSTRAINT CK_NotificationSmtpSettings_Port CHECK (Port BETWEEN 1 AND 65535),
    Username NVARCHAR(255) NULL,
    EncryptedPassword NVARCHAR(MAX) NULL,
    FromAddress NVARCHAR(320) NULL,
    FromDisplayName NVARCHAR(200) NOT NULL CONSTRAINT DF_NotificationSmtpSettings_FromDisplayName DEFAULT ('AScheduler Notifications'),
    EnableSsl BIT NOT NULL CONSTRAINT DF_NotificationSmtpSettings_EnableSsl DEFAULT (1),
    UpdatedByUserId INT NULL,
    CreatedAtUtc DATETIME2 NOT NULL CONSTRAINT DF_NotificationSmtpSettings_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
    UpdatedAtUtc DATETIME2 NOT NULL CONSTRAINT DF_NotificationSmtpSettings_UpdatedAtUtc DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT FK_NotificationSmtpSettings_Users FOREIGN KEY (UpdatedByUserId) REFERENCES dbo.Users(UserId)
);

INSERT INTO dbo.NotificationSmtpSettings
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
    0,
    NULL,
    587,
    NULL,
    NULL,
    'noreply@ascheduler.local',
    'AScheduler Notifications',
    1,
    NULL,
    SYSUTCDATETIME(),
    SYSUTCDATETIME()
);

PRINT 'Notification settings created.';
