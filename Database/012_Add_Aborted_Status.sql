-- Module 3: Add 'Aborted' execution status.
-- Executions interrupted by a server crash/restart are now marked as 'Aborted'
-- instead of 'Failed', keeping system interruptions separate from real task failures.

-- Drop the existing lifecycle constraint and recreate it with 'Aborted' allowed.
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
        (Status IN ('Success', 'Failed', 'Aborted') AND StartedAt IS NOT NULL AND EndedAt IS NOT NULL)
    );
GO

PRINT 'Migration 012: CK_TaskExecutions_StatusLifecycle updated to allow Aborted status.';
GO
