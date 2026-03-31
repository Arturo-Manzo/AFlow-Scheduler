IF OBJECT_ID('dbo.TaskExecutionLogs', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.TaskExecutionLogs (
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

    CREATE INDEX IX_TaskExecutionLogs_TaskExecutionId_Timestamp
        ON dbo.TaskExecutionLogs(TaskExecutionId, TimestampUtc ASC);

    CREATE INDEX IX_TaskExecutionLogs_BoxRunId_Timestamp
        ON dbo.TaskExecutionLogs(BoxRunId, TimestampUtc ASC)
        WHERE BoxRunId IS NOT NULL;

    PRINT 'TaskExecutionLogs table created successfully';
END
ELSE
BEGIN
    PRINT 'TaskExecutionLogs table already exists';
END