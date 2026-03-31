-- Module 2: Execution observability and control.
-- Ensures TaskExecutions supports running-state persistence, stale recovery,
-- and consistency constraints for final statuses.

IF EXISTS (
    SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID(N'dbo.TaskExecutions')
      AND name = N'BoxRunId'
      AND is_nullable = 0
)
BEGIN
    ALTER TABLE dbo.TaskExecutions DROP CONSTRAINT FK_TaskExecutions_BoxRuns;
    ALTER TABLE dbo.TaskExecutions ALTER COLUMN BoxRunId INT NULL;
    ALTER TABLE dbo.TaskExecutions
        ADD CONSTRAINT FK_TaskExecutions_BoxRuns
            FOREIGN KEY (BoxRunId) REFERENCES dbo.BoxRuns(BoxRunId) ON DELETE CASCADE;
END
GO

IF EXISTS (
    SELECT 1 FROM sys.check_constraints
    WHERE parent_object_id = OBJECT_ID(N'dbo.TaskExecutions')
      AND name = N'CK_TaskExecutions_StatusLifecycle'
)
BEGIN
    ALTER TABLE dbo.TaskExecutions DROP CONSTRAINT CK_TaskExecutions_StatusLifecycle;
END
GO

ALTER TABLE dbo.TaskExecutions
    ADD CONSTRAINT CK_TaskExecutions_StatusLifecycle CHECK (
        (Status = 'Running' AND StartedAt IS NOT NULL AND EndedAt IS NULL)
        OR
        (Status IN ('Success', 'Failed') AND StartedAt IS NOT NULL AND EndedAt IS NOT NULL)
    );
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.TaskExecutions')
      AND name = N'UX_TaskExecutions_Running_BoxRunTask'
)
BEGIN
    CREATE UNIQUE INDEX UX_TaskExecutions_Running_BoxRunTask
        ON dbo.TaskExecutions(TaskId, BoxRunId)
        WHERE BoxRunId IS NOT NULL AND Status = 'Running';
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.TaskExecutions')
      AND name = N'UX_TaskExecutions_Running_ForceStartTask'
)
BEGIN
    CREATE UNIQUE INDEX UX_TaskExecutions_Running_ForceStartTask
        ON dbo.TaskExecutions(TaskId, TriggerSource)
        WHERE BoxRunId IS NULL AND TriggerSource = 'ForceStart' AND Status = 'Running';
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.TaskExecutions')
      AND name = N'IX_TaskExecutions_Status_StartedAt'
)
BEGIN
    CREATE INDEX IX_TaskExecutions_Status_StartedAt
        ON dbo.TaskExecutions(Status, StartedAt);
END
GO

PRINT 'Migration 011 applied: execution observability enabled.';