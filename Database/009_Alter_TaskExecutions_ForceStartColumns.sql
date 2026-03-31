-- Migration 009: Add RequestedByUserId and Reason columns to TaskExecutions,
-- and add CHECK constraint enforcing the BoxRunId / TriggerSource invariant.
--
-- Rules enforced by the CHECK constraint:
--   * BoxRun-triggered executions (Scheduled / Manual / Waiting) must have a BoxRunId.
--   * ForceStart executions must NOT have a BoxRunId.
--
-- Safe to re-run: each ALTER is guarded by a column-existence check.

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID(N'dbo.TaskExecutions')
      AND name = N'RequestedByUserId'
)
BEGIN
    ALTER TABLE dbo.TaskExecutions
        ADD RequestedByUserId INT NULL;
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID(N'dbo.TaskExecutions')
      AND name = N'Reason'
)
BEGIN
    ALTER TABLE dbo.TaskExecutions
        ADD Reason NVARCHAR(500) NULL;
END
GO

-- Drop the constraint first so this script is idempotent.
IF EXISTS (
    SELECT 1 FROM sys.check_constraints
    WHERE parent_object_id = OBJECT_ID(N'dbo.TaskExecutions')
      AND name = N'CK_TaskExecutions_BoxRunId_TriggerSource'
)
BEGIN
    ALTER TABLE dbo.TaskExecutions
        DROP CONSTRAINT CK_TaskExecutions_BoxRunId_TriggerSource;
END
GO

ALTER TABLE dbo.TaskExecutions
    ADD CONSTRAINT CK_TaskExecutions_BoxRunId_TriggerSource CHECK (
        (BoxRunId IS NOT NULL AND TriggerSource IN ('Scheduled', 'Manual', 'Waiting'))
        OR
        (BoxRunId IS NULL AND TriggerSource = 'ForceStart')
    );
GO
