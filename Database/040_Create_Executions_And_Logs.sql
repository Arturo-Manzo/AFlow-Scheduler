-- ============================================
-- 040_Create_Executions_And_Logs.sql
-- Task execution history and low-level logs.
-- ============================================

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;

CREATE TABLE dbo.TaskExecutions
(
    ExecutionId INT IDENTITY(1,1) PRIMARY KEY,
    TaskId INT NOT NULL,
    BoxRunId INT NULL,
    StartedAt DATETIME2 NOT NULL,
    EndedAt DATETIME2 NULL,
    Status NVARCHAR(20) NOT NULL,
    Output NVARCHAR(MAX) NULL,
    Error NVARCHAR(MAX) NULL,
    ExitCode INT NULL,
    StdOut NVARCHAR(MAX) NULL,
    StdErr NVARCHAR(MAX) NULL,
    TriggerSource NVARCHAR(20) NOT NULL CONSTRAINT DF_TaskExecutions_TriggerSource DEFAULT 'Scheduler',
    ScheduledForUtc DATETIME2 NULL,
    RequestedByUserId INT NULL,
    Reason NVARCHAR(500) NULL,
    CONSTRAINT FK_TaskExecutions_Tasks FOREIGN KEY (TaskId) REFERENCES dbo.Tasks(TaskId),
    CONSTRAINT FK_TaskExecutions_BoxRuns FOREIGN KEY (BoxRunId) REFERENCES dbo.BoxRuns(BoxRunId) ON DELETE CASCADE,
    CONSTRAINT FK_TaskExecutions_Users FOREIGN KEY (RequestedByUserId) REFERENCES dbo.Users(UserId),
    CONSTRAINT CK_TaskExecutions_BoxRunId_TriggerSource CHECK (
        (BoxRunId IS NOT NULL AND TriggerSource IN ('Scheduler', 'Manual', 'Retry', 'Scheduled'))
        OR (BoxRunId IS NULL AND TriggerSource = 'ForceStart')
    ),
    CONSTRAINT CK_TaskExecutions_StatusLifecycle CHECK (
        (Status = 'Running' AND StartedAt IS NOT NULL AND EndedAt IS NULL)
        OR (Status IN ('Success', 'Failed', 'Aborted', 'NotExecuted', 'Skipped') AND StartedAt IS NOT NULL AND EndedAt IS NOT NULL)
    )
);

CREATE INDEX IX_TaskExecutions_TaskId_Started ON dbo.TaskExecutions(TaskId, StartedAt DESC);
CREATE INDEX IX_TaskExecutions_BoxRunId ON dbo.TaskExecutions(BoxRunId);
CREATE INDEX IX_TaskExecutions_Status_StartedAt ON dbo.TaskExecutions(Status, StartedAt);

CREATE UNIQUE INDEX UX_TaskExecutions_Running_BoxRunTask
    ON dbo.TaskExecutions(TaskId, BoxRunId)
    WHERE BoxRunId IS NOT NULL AND Status = 'Running';

CREATE UNIQUE INDEX UX_TaskExecutions_Running_ForceStartTask
    ON dbo.TaskExecutions(TaskId, TriggerSource)
    WHERE BoxRunId IS NULL AND TriggerSource = 'ForceStart' AND Status = 'Running';

CREATE TABLE dbo.TaskExecutionLogs
(
    Id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    BoxRunId INT NULL,
    TaskId INT NOT NULL,
    TaskExecutionId INT NOT NULL,
    TimestampUtc DATETIME2 NOT NULL,
    Level NVARCHAR(20) NOT NULL,
    Message NVARCHAR(1000) NOT NULL,
    Details NVARCHAR(MAX) NULL,
    CONSTRAINT FK_TaskExecutionLogs_BoxRuns FOREIGN KEY (BoxRunId) REFERENCES dbo.BoxRuns(BoxRunId),
    CONSTRAINT FK_TaskExecutionLogs_Tasks FOREIGN KEY (TaskId) REFERENCES dbo.Tasks(TaskId),
    CONSTRAINT FK_TaskExecutionLogs_TaskExecutions FOREIGN KEY (TaskExecutionId) REFERENCES dbo.TaskExecutions(ExecutionId) ON DELETE CASCADE
);

CREATE INDEX IX_TaskExecutionLogs_TaskExecutionId_Timestamp ON dbo.TaskExecutionLogs(TaskExecutionId, TimestampUtc ASC);
CREATE INDEX IX_TaskExecutionLogs_BoxRunId_Timestamp ON dbo.TaskExecutionLogs(BoxRunId, TimestampUtc ASC) WHERE BoxRunId IS NOT NULL;

PRINT 'Executions and task logs created.';
