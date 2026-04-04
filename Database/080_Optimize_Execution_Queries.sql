-- ============================================
-- 080_Optimize_Execution_Queries.sql
-- Production-oriented index tuning for frequent execution queries.
-- Safe to run multiple times.
-- ============================================

SET NOCOUNT ON;
SET XACT_ABORT ON;

-- Speeds latest/history lookups and common ORDER BY StartedAt DESC patterns.
IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = 'IX_TaskExecutions_StartedAt_Desc'
      AND object_id = OBJECT_ID('dbo.TaskExecutions')
)
BEGIN
    CREATE INDEX IX_TaskExecutions_StartedAt_Desc
        ON dbo.TaskExecutions(StartedAt DESC)
        INCLUDE (TaskId, BoxRunId, Status, TriggerSource, ExitCode, EndedAt);
END

-- Speeds retrieval of non-success executions by recency.
IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = 'IX_TaskExecutions_NonSuccess_Recent'
      AND object_id = OBJECT_ID('dbo.TaskExecutions')
)
BEGIN
    CREATE INDEX IX_TaskExecutions_NonSuccess_Recent
        ON dbo.TaskExecutions(Status, StartedAt DESC)
        INCLUDE (TaskId, BoxRunId, TriggerSource, ExitCode, EndedAt)
        WHERE Status IN ('Failed', 'Aborted', 'NotExecuted', 'Skipped');
END

-- Speeds force-start monitoring and stale running checks.
IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = 'IX_TaskExecutions_Running_StartedAt'
      AND object_id = OBJECT_ID('dbo.TaskExecutions')
)
BEGIN
    CREATE INDEX IX_TaskExecutions_Running_StartedAt
        ON dbo.TaskExecutions(StartedAt ASC)
        INCLUDE (TaskId, BoxRunId, TriggerSource)
        WHERE Status = 'Running';
END

PRINT 'Execution query optimization completed.';
