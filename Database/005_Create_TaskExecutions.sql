-- ============================================
-- TaskExecutions Table
-- Stores one record per task step execution within a BoxRun.
-- ============================================

IF OBJECT_ID('dbo.vw_TaskExecutionHistory', 'V') IS NOT NULL DROP VIEW dbo.vw_TaskExecutionHistory;
IF OBJECT_ID('dbo.TaskExecutions', 'U') IS NOT NULL DROP TABLE dbo.TaskExecutions;

CREATE TABLE TaskExecutions (
    ExecutionId     INT IDENTITY(1,1) PRIMARY KEY,
    TaskId          INT NOT NULL,
    BoxRunId        INT NOT NULL,
    StartedAt       DATETIME2 NOT NULL,
    EndedAt         DATETIME2 NULL,
    Status          NVARCHAR(20) NOT NULL,
    Output          NVARCHAR(MAX) NULL,
    Error           NVARCHAR(MAX) NULL,
    ExitCode        INT NULL,
    StdOut          NVARCHAR(MAX) NULL,
    StdErr          NVARCHAR(MAX) NULL,
    TriggerSource   NVARCHAR(20) NOT NULL DEFAULT 'Scheduled',
    ScheduledForUtc DATETIME2 NULL,
    CONSTRAINT FK_TaskExecutions_Tasks   FOREIGN KEY (TaskId)   REFERENCES Tasks(TaskId),
    CONSTRAINT FK_TaskExecutions_BoxRuns FOREIGN KEY (BoxRunId) REFERENCES BoxRuns(BoxRunId) ON DELETE CASCADE
);
CREATE INDEX IX_TaskExecutions_TaskId_Started ON TaskExecutions(TaskId, StartedAt DESC);
CREATE INDEX IX_TaskExecutions_BoxRunId        ON TaskExecutions(BoxRunId);
PRINT 'TaskExecutions table recreated successfully';
