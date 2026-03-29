-- ============================================
-- AuditLog Table
-- ============================================

IF OBJECT_ID('dbo.AuditLog', 'U') IS NOT NULL
    DROP TABLE dbo.AuditLog;

CREATE TABLE AuditLog (
    AuditId   INT IDENTITY(1,1) PRIMARY KEY,
    UserId    INT NOT NULL,
    TableName NVARCHAR(100) NOT NULL,
    RecordId  INT NOT NULL,
    Action    NVARCHAR(20) NOT NULL,
    OldValues NVARCHAR(MAX) NULL,
    NewValues NVARCHAR(MAX) NULL,
    Reason    NVARCHAR(255) NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    FOREIGN KEY (UserId) REFERENCES Users(UserId)
);

CREATE INDEX IX_AuditLog_UserId    ON AuditLog(UserId);
CREATE INDEX IX_AuditLog_CreatedAt ON AuditLog(CreatedAt);
CREATE INDEX IX_AuditLog_Action    ON AuditLog(Action);

PRINT 'AuditLog table recreated successfully';
