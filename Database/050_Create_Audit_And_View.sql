-- ============================================
-- 050_Create_Audit_And_View.sql
-- Audit trail and consolidated execution-history view.
-- ============================================

CREATE TABLE dbo.AuditLog
(
    AuditId INT IDENTITY(1,1) PRIMARY KEY,
    UserId INT NOT NULL,
    TableName NVARCHAR(100) NOT NULL,
    RecordId INT NOT NULL,
    Action NVARCHAR(20) NOT NULL,
    OldValues NVARCHAR(MAX) NULL,
    NewValues NVARCHAR(MAX) NULL,
    Reason NVARCHAR(255) NULL,
    CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_AuditLog_CreatedAt DEFAULT SYSUTCDATETIME(),
    CONSTRAINT FK_AuditLog_Users FOREIGN KEY (UserId) REFERENCES dbo.Users(UserId)
);

CREATE INDEX IX_AuditLog_UserId ON dbo.AuditLog(UserId);
CREATE INDEX IX_AuditLog_CreatedAt ON dbo.AuditLog(CreatedAt);
CREATE INDEX IX_AuditLog_Action ON dbo.AuditLog(Action);

EXEC(N'
CREATE VIEW dbo.vw_TaskExecutionHistory
AS
SELECT
    te.ExecutionId,
    t.TaskId,
    t.Name AS TaskName,
    b.BoxId,
    b.Name AS BoxName,
    br.BoxRunId,
    br.ScheduledForUtc AS BoxScheduledForUtc,
    te.StartedAt,
    te.EndedAt,
    DATEDIFF(SECOND, te.StartedAt, ISNULL(te.EndedAt, SYSUTCDATETIME())) AS ExecutionSeconds,
    te.Status,
    te.ExitCode,
    te.TriggerSource,
    te.ScheduledForUtc
FROM dbo.TaskExecutions te
INNER JOIN dbo.Tasks t ON te.TaskId = t.TaskId
INNER JOIN dbo.Boxes b ON t.BoxId = b.BoxId
LEFT JOIN dbo.BoxRuns br ON te.BoxRunId = br.BoxRunId
');

PRINT 'Audit and view created.';
