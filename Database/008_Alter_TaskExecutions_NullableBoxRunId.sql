-- ============================================
-- Migration 008: Allow NULL BoxRunId in TaskExecutions
--
-- Reason: TaskForceStart executes a single task in isolation without a BoxRun.
-- Its result is stored in TaskExecutions with BoxRunId = NULL.
-- NULL values are excluded from FK enforcement in SQL Server, so the FK is valid.
--
-- IMPORTANT: History queries that use INNER JOIN on BoxRuns will NOT return
-- ForceStart records. This is intentional — they are tracked independently.
-- ============================================

-- Drop the existing NOT NULL FK constraint
ALTER TABLE TaskExecutions DROP CONSTRAINT FK_TaskExecutions_BoxRuns;

-- Allow NULL values in BoxRunId
ALTER TABLE TaskExecutions ALTER COLUMN BoxRunId INT NULL;

-- Recreate the FK constraint (SQL Server ignores NULL values in FK checks)
ALTER TABLE TaskExecutions
    ADD CONSTRAINT FK_TaskExecutions_BoxRuns
    FOREIGN KEY (BoxRunId) REFERENCES BoxRuns(BoxRunId) ON DELETE CASCADE;

PRINT 'Migration 008 applied: TaskExecutions.BoxRunId is now nullable.';
