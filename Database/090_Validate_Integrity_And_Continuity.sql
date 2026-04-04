-- ============================================
-- 090_Validate_Integrity_And_Continuity.sql
-- Post-deployment validation checks for data continuity.
-- Throws when critical integrity issues are detected.
-- ============================================

SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @issueCount INT = 0;

-- 1) Running executions must not have EndedAt.
IF EXISTS (
    SELECT 1
    FROM dbo.TaskExecutions
    WHERE Status = 'Running'
      AND EndedAt IS NOT NULL
)
BEGIN
    SET @issueCount += 1;
    PRINT 'Integrity issue: Running executions with EndedAt populated.';
END

-- 2) Terminal executions must have EndedAt.
IF EXISTS (
    SELECT 1
    FROM dbo.TaskExecutions
    WHERE Status IN ('Success', 'Failed', 'Aborted', 'NotExecuted', 'Skipped')
      AND EndedAt IS NULL
)
BEGIN
    SET @issueCount += 1;
    PRINT 'Integrity issue: Terminal executions without EndedAt.';
END

-- 3) BoxRuns marked Completed should not have active Running executions.
IF EXISTS (
    SELECT 1
    FROM dbo.BoxRuns br
    INNER JOIN dbo.TaskExecutions te ON te.BoxRunId = br.BoxRunId
    WHERE br.Status IN ('Completed', 'Partial', 'Failed', 'Cancelled')
      AND te.Status = 'Running'
)
BEGIN
    SET @issueCount += 1;
    PRINT 'Continuity issue: Finalized BoxRuns still have Running executions.';
END

-- 4) Orphan logs should be impossible (FK-protected), but validate anyway.
IF EXISTS (
    SELECT 1
    FROM dbo.TaskExecutionLogs l
    LEFT JOIN dbo.TaskExecutions te ON te.ExecutionId = l.TaskExecutionId
    WHERE te.ExecutionId IS NULL
)
BEGIN
    SET @issueCount += 1;
    PRINT 'Integrity issue: Orphan TaskExecutionLogs found.';
END

IF @issueCount > 0
BEGIN
    THROW 52000, 'Integrity and continuity validation failed. See previous messages.', 1;
END

PRINT 'Integrity and continuity validation passed.';
