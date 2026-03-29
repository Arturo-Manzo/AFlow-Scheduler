-- ============================================
-- Execution History View
-- (TriggerSource and ScheduledForUtc are built into the
--  base CREATE TABLE scripts above; this script creates the view.)
-- ============================================

DROP VIEW IF EXISTS vw_TaskExecutionHistory;
GO

CREATE VIEW vw_TaskExecutionHistory AS
SELECT
    te.ExecutionId,
    t.TaskId,
    t.Name          AS TaskName,
    b.BoxId,
    b.Name          AS BoxName,
    br.BoxRunId,
    br.ScheduledForUtc AS BoxScheduledForUtc,
    te.StartedAt,
    te.EndedAt,
    DATEDIFF(SECOND, te.StartedAt, ISNULL(te.EndedAt, SYSUTCDATETIME())) AS ExecutionSeconds,
    te.Status,
    te.ExitCode,
    te.TriggerSource,
    te.ScheduledForUtc
FROM       TaskExecutions te
INNER JOIN Tasks   t  ON te.TaskId   = t.TaskId
INNER JOIN Boxes   b  ON t.BoxId     = b.BoxId
LEFT  JOIN BoxRuns br ON te.BoxRunId = br.BoxRunId;
GO

PRINT 'vw_TaskExecutionHistory view created successfully';