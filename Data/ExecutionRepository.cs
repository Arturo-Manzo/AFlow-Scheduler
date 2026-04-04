using System.Data;
using Microsoft.Data.SqlClient;
using Dapper;
using Microsoft.Extensions.Configuration;
using AScheduler.Domain;

namespace AScheduler.Data
{
    public class ExecutionRepository : IExecutionRepository
    {
        private readonly string _connectionString;

        public ExecutionRepository(IConfiguration config)
        {
            ArgumentNullException.ThrowIfNull(config);
            _connectionString = config.GetConnectionString("Default")!;
        }

        private IDbConnection CreateConnection() => new SqlConnection(_connectionString);

        public async Task<List<ExecutionRecord>> GetExecutionsForBoxRunAsync(int boxRunId)
        {
            using var connection = CreateConnection();
            const string sql = @"
                SELECT te.ExecutionId, te.TaskId, te.BoxRunId, t.BoxId, b.Name AS BoxName,
                       b.TimeZoneId AS BoxTimeZoneId,
                      d.Name AS DepartmentName,
                      b.NotificationEmail AS FailureAlertEmail,
                       t.Name AS TaskName, t.TaskType, t.Command, te.StartedAt, te.EndedAt, te.Status,
                       te.Output, te.Error, te.ExitCode, te.StdOut, te.StdErr,
                       te.TriggerSource, te.ScheduledForUtc, te.Reason,
                       CAST(CASE WHEN te.Status = 'Running' AND te.StartedAt < @StaleBeforeUtc THEN 1 ELSE 0 END AS bit) AS IsStale,
                       COALESCE(te.RequestedByUserId, br.RequestedByUserId) AS RequestedByUserId,
                       u.Username AS RequestedByUsername
                FROM TaskExecutions te
                INNER JOIN Tasks t ON te.TaskId = t.TaskId
                INNER JOIN Boxes b ON t.BoxId = b.BoxId
                LEFT JOIN Departments d ON b.DepartmentId = d.DepartmentId
                INNER JOIN BoxRuns br ON te.BoxRunId = br.BoxRunId
                LEFT JOIN Users u ON COALESCE(te.RequestedByUserId, br.RequestedByUserId) = u.UserId
                WHERE te.BoxRunId = @BoxRunId
                ORDER BY te.StartedAt ASC";
            var result = await connection.QueryAsync<ExecutionRecord>(sql, new
            {
                BoxRunId = boxRunId,
                StaleBeforeUtc = DateTime.UtcNow.AddYears(-10)
            });
            return result.Select(NormalizeRecord).ToList();
        }

        public async Task<List<ExecutionRecord>> GetExecutionsForTaskAsync(int taskId, DateTime? fromUtc = null, DateTime? toUtc = null)
        {
            using var connection = CreateConnection();
            const string sql = @"
                SELECT te.ExecutionId, te.TaskId, te.BoxRunId, t.BoxId, b.Name AS BoxName,
                       b.TimeZoneId AS BoxTimeZoneId,
                      d.Name AS DepartmentName,
                      b.NotificationEmail AS FailureAlertEmail,
                       t.Name AS TaskName, t.TaskType, t.Command, te.StartedAt, te.EndedAt, te.Status,
                       te.Output, te.Error, te.ExitCode, te.StdOut, te.StdErr,
                       te.TriggerSource, te.ScheduledForUtc, te.Reason,
                       CAST(CASE WHEN te.Status = 'Running' AND te.StartedAt < @StaleBeforeUtc THEN 1 ELSE 0 END AS bit) AS IsStale,
                       COALESCE(te.RequestedByUserId, br.RequestedByUserId) AS RequestedByUserId,
                       u.Username AS RequestedByUsername
                FROM TaskExecutions te
                INNER JOIN Tasks t ON te.TaskId = t.TaskId
                INNER JOIN Boxes b ON t.BoxId = b.BoxId
                LEFT JOIN Departments d ON b.DepartmentId = d.DepartmentId
                LEFT JOIN BoxRuns br ON te.BoxRunId = br.BoxRunId
                LEFT JOIN Users u ON COALESCE(te.RequestedByUserId, br.RequestedByUserId) = u.UserId
                WHERE te.TaskId = @TaskId
                  AND (@FromUtc IS NULL OR te.StartedAt >= @FromUtc)
                  AND (@ToUtc IS NULL OR te.StartedAt <= @ToUtc)
                ORDER BY te.StartedAt DESC";
            var result = await connection.QueryAsync<ExecutionRecord>(sql, new
            {
                TaskId = taskId,
                FromUtc = fromUtc,
                ToUtc = toUtc,
                StaleBeforeUtc = DateTime.UtcNow.AddYears(-10)
            });
            return result.Select(NormalizeRecord).ToList();
        }

        public async Task<ExecutionRecord?> GetExecutionByIdAsync(int executionId)
        {
            using var connection = CreateConnection();
            const string sql = @"
                SELECT te.ExecutionId, te.TaskId, te.BoxRunId, t.BoxId, b.Name AS BoxName,
                       b.TimeZoneId AS BoxTimeZoneId,
                      d.Name AS DepartmentName,
                      b.NotificationEmail AS FailureAlertEmail,
                       t.Name AS TaskName, t.TaskType, t.Command, te.StartedAt, te.EndedAt, te.Status,
                       te.Output, te.Error, te.ExitCode, te.StdOut, te.StdErr,
                       te.TriggerSource, te.ScheduledForUtc, te.Reason,
                       CAST(0 AS bit) AS IsStale,
                       COALESCE(te.RequestedByUserId, br.RequestedByUserId) AS RequestedByUserId,
                       u.Username AS RequestedByUsername
                FROM TaskExecutions te
                INNER JOIN Tasks t ON te.TaskId = t.TaskId
                INNER JOIN Boxes b ON t.BoxId = b.BoxId
                LEFT JOIN Departments d ON b.DepartmentId = d.DepartmentId
                LEFT JOIN BoxRuns br ON te.BoxRunId = br.BoxRunId
                LEFT JOIN Users u ON COALESCE(te.RequestedByUserId, br.RequestedByUserId) = u.UserId
                WHERE te.ExecutionId = @ExecutionId";

            var record = await connection.QueryFirstOrDefaultAsync<ExecutionRecord>(sql, new { ExecutionId = executionId });
            return record == null ? null : NormalizeRecord(record);
        }

        public async Task<ExecutionRecord?> GetLastExecutionForTaskAsync(int taskId)
        {
            using var connection = CreateConnection();
            const string sql = @"
                SELECT TOP 1 te.ExecutionId, te.TaskId, te.BoxRunId, t.BoxId, b.Name AS BoxName,
                       b.TimeZoneId AS BoxTimeZoneId,
                      d.Name AS DepartmentName,
                      b.NotificationEmail AS FailureAlertEmail,
                       t.Name AS TaskName, t.TaskType, t.Command, te.StartedAt, te.EndedAt, te.Status,
                       te.Output, te.Error, te.ExitCode, te.StdOut, te.StdErr,
                       te.TriggerSource, te.ScheduledForUtc, te.Reason,
                       CAST(0 AS bit) AS IsStale,
                       COALESCE(te.RequestedByUserId, br.RequestedByUserId) AS RequestedByUserId,
                       u.Username AS RequestedByUsername
                FROM TaskExecutions te
                INNER JOIN Tasks t ON te.TaskId = t.TaskId
                INNER JOIN Boxes b ON t.BoxId = b.BoxId
                LEFT JOIN Departments d ON b.DepartmentId = d.DepartmentId
                LEFT JOIN BoxRuns br ON te.BoxRunId = br.BoxRunId
                LEFT JOIN Users u ON COALESCE(te.RequestedByUserId, br.RequestedByUserId) = u.UserId
                WHERE te.TaskId = @TaskId
                ORDER BY te.StartedAt DESC, te.ExecutionId DESC";
            var record = await connection.QueryFirstOrDefaultAsync<ExecutionRecord>(sql, new { TaskId = taskId });
            return record == null ? null : NormalizeRecord(record);
        }

        public async Task<List<ExecutionRecord>> GetRunningExecutionsAsync(DateTime staleBeforeUtc)
        {
            using var connection = CreateConnection();
            const string sql = @"
                SELECT te.ExecutionId, te.TaskId, te.BoxRunId, t.BoxId, b.Name AS BoxName,
                       b.TimeZoneId AS BoxTimeZoneId,
                      d.Name AS DepartmentName,
                      b.NotificationEmail AS FailureAlertEmail,
                       t.Name AS TaskName, t.TaskType, t.Command, te.StartedAt, te.EndedAt, te.Status,
                       te.Output, te.Error, te.ExitCode, te.StdOut, te.StdErr,
                       te.TriggerSource, te.ScheduledForUtc, te.Reason,
                       CAST(CASE WHEN te.StartedAt < @StaleBeforeUtc THEN 1 ELSE 0 END AS bit) AS IsStale,
                       COALESCE(te.RequestedByUserId, br.RequestedByUserId) AS RequestedByUserId,
                       u.Username AS RequestedByUsername
                FROM TaskExecutions te
                INNER JOIN Tasks t ON te.TaskId = t.TaskId
                INNER JOIN Boxes b ON t.BoxId = b.BoxId
                LEFT JOIN Departments d ON b.DepartmentId = d.DepartmentId
                LEFT JOIN BoxRuns br ON te.BoxRunId = br.BoxRunId
                LEFT JOIN Users u ON COALESCE(te.RequestedByUserId, br.RequestedByUserId) = u.UserId
                WHERE te.Status = 'Running'
                ORDER BY te.StartedAt ASC";
            var result = await connection.QueryAsync<ExecutionRecord>(sql, new { StaleBeforeUtc = staleBeforeUtc });
            return result.Select(NormalizeRecord).ToList();
        }

        public async Task<int> CreateExecutionAsync(int taskId, int? boxRunId, DateTime startedAtUtc,
            string triggerSource, DateTime? scheduledForUtc, int? requestedByUserId, string? reason)
        {
            using var connection = CreateConnection();
            var normalizedTriggerSource = TriggerSources.Normalize(triggerSource);
            const string sql = @"
                INSERT INTO TaskExecutions
                    (TaskId, BoxRunId, StartedAt, EndedAt, Status, Output, Error, ExitCode,
                     StdOut, StdErr, TriggerSource, ScheduledForUtc, RequestedByUserId, Reason)
                VALUES
                    (@TaskId, @BoxRunId, @StartedAtUtc, NULL, 'Running', '', NULL, NULL,
                     '', '', @TriggerSource, @ScheduledForUtc, @RequestedByUserId, @Reason);
                SELECT CAST(SCOPE_IDENTITY() AS INT);";
            return await connection.ExecuteScalarAsync<int>(sql, new
            {
                TaskId = taskId,
                BoxRunId = boxRunId,
                StartedAtUtc = startedAtUtc,
                TriggerSource = normalizedTriggerSource,
                ScheduledForUtc = scheduledForUtc,
                RequestedByUserId = requestedByUserId,
                Reason = reason
            });
        }

        public async Task CompleteExecutionAsync(int executionId, DateTime endedAtUtc, string status,
            string output, string error, int? exitCode, string stdOut, string stdErr)
        {
            ValidateTerminalStatus(status);
            using var connection = CreateConnection();
            const string sql = @"
                UPDATE TaskExecutions
                SET EndedAt = @EndedAtUtc,
                    Status = @Status,
                    Output = @Output,
                    Error = @Error,
                    ExitCode = @ExitCode,
                    StdOut = @StdOut,
                    StdErr = @StdErr
                WHERE ExecutionId = @ExecutionId
                  AND Status = 'Running'
                  AND EndedAt IS NULL";
            var rows = await connection.ExecuteAsync(sql, new
            {
                ExecutionId = executionId,
                EndedAtUtc = endedAtUtc,
                Status = status,
                Output = output,
                Error = error,
                ExitCode = exitCode,
                StdOut = stdOut,
                StdErr = stdErr
            });

            if (rows != 1)
            {
                throw new InvalidOperationException(
                    $"Invalid execution state transition for ExecutionId {executionId}. Only Running executions can transition to terminal states.");
            }
        }

        public async Task<int> AbortRunningExecutionsAsync(DateTime endedAtUtc, string reason)
        {
            using var connection = CreateConnection();
            const string sql = @"
                UPDATE TaskExecutions
                SET Status  = 'Aborted',
                    EndedAt = @EndedAtUtc,
                    Error   = @Reason,
                    StdErr  = @Reason
                WHERE Status = 'Running'
                  AND EndedAt IS NULL;
                SELECT @@ROWCOUNT;";
            return await connection.ExecuteScalarAsync<int>(sql, new { EndedAtUtc = endedAtUtc, Reason = reason });
        }

        public async Task<ExecutionRecord?> GetLastExecutionForTaskInBoxRunAsync(int taskId, int boxRunId)
        {
            using var connection = CreateConnection();
            const string sql = @"
                SELECT TOP 1 te.ExecutionId, te.TaskId, te.BoxRunId, t.BoxId, b.Name AS BoxName,
                       b.TimeZoneId AS BoxTimeZoneId,
                      d.Name AS DepartmentName,
                      b.NotificationEmail AS FailureAlertEmail,
                       t.Name AS TaskName, t.TaskType, t.Command, te.StartedAt, te.EndedAt, te.Status,
                       te.Output, te.Error, te.ExitCode, te.StdOut, te.StdErr,
                       te.TriggerSource, te.ScheduledForUtc, te.Reason,
                       CAST(0 AS bit) AS IsStale,
                       COALESCE(te.RequestedByUserId, br.RequestedByUserId) AS RequestedByUserId,
                       u.Username AS RequestedByUsername
                FROM TaskExecutions te
                INNER JOIN Tasks t ON te.TaskId = t.TaskId
                INNER JOIN Boxes b ON t.BoxId = b.BoxId
                LEFT JOIN Departments d ON b.DepartmentId = d.DepartmentId
                INNER JOIN BoxRuns br ON te.BoxRunId = br.BoxRunId
                LEFT JOIN Users u ON COALESCE(te.RequestedByUserId, br.RequestedByUserId) = u.UserId
                WHERE te.TaskId = @TaskId AND te.BoxRunId = @BoxRunId
                ORDER BY te.EndedAt DESC, te.StartedAt DESC";
            var record = await connection.QueryFirstOrDefaultAsync<ExecutionRecord>(sql, new { TaskId = taskId, BoxRunId = boxRunId });
            return record == null ? null : NormalizeRecord(record);
        }

        public async Task<List<ExecutionRecord>> GetLatestExecutionsAsync(int limit = 20)
        {
            if (limit <= 0) limit = 20;
            using var connection = CreateConnection();
            const string sql = @"
                SELECT TOP (@Limit)
                       te.ExecutionId, te.TaskId, te.BoxRunId, t.BoxId, b.Name AS BoxName,
                       b.TimeZoneId AS BoxTimeZoneId,
                      d.Name AS DepartmentName,
                      b.NotificationEmail AS FailureAlertEmail,
                       t.Name AS TaskName, t.TaskType, t.Command, te.StartedAt, te.EndedAt, te.Status,
                       te.Output, te.Error, te.ExitCode, te.StdOut, te.StdErr,
                       te.TriggerSource, te.ScheduledForUtc, te.Reason,
                       CAST(CASE WHEN te.Status = 'Running' AND te.StartedAt < @StaleBeforeUtc THEN 1 ELSE 0 END AS bit) AS IsStale,
                       COALESCE(te.RequestedByUserId, br.RequestedByUserId) AS RequestedByUserId,
                       u.Username AS RequestedByUsername
                FROM TaskExecutions te
                INNER JOIN Tasks t ON te.TaskId = t.TaskId
                INNER JOIN Boxes b ON t.BoxId = b.BoxId
                LEFT JOIN Departments d ON b.DepartmentId = d.DepartmentId
                LEFT JOIN BoxRuns br ON te.BoxRunId = br.BoxRunId
                LEFT JOIN Users u ON COALESCE(te.RequestedByUserId, br.RequestedByUserId) = u.UserId
                ORDER BY te.StartedAt DESC";
            var result = await connection.QueryAsync<ExecutionRecord>(sql, new
            {
                Limit = limit,
                StaleBeforeUtc = DateTime.UtcNow.AddYears(-10)
            });
            return result.Select(NormalizeRecord).ToList();
        }

        public async Task<List<ExecutionRecord>> GetFailedExecutionsAsync(int limit = 50, int? boxId = null, DateTime? fromUtc = null, DateTime? toUtc = null, int? departmentId = null, string[]? status = null, string? taskName = null, string? triggerSource = null)
        {
            if (limit <= 0) limit = 50;
            using var connection = CreateConnection();

            // Default to original behaviour when no status filter is provided
            var allowedStatuses = (status is { Length: > 0 })
                ? status
                : new[] { "Failed", "Aborted", "NotExecuted", "Skipped" };

            var sql = $@"
                SELECT TOP (@Limit)
                       te.ExecutionId, te.TaskId, te.BoxRunId, t.BoxId, b.Name AS BoxName,
                       b.TimeZoneId AS BoxTimeZoneId,
                      d.Name AS DepartmentName,
                      b.NotificationEmail AS FailureAlertEmail,
                       t.Name AS TaskName, t.TaskType, t.Command, te.StartedAt, te.EndedAt, te.Status,
                       te.Output, te.Error, te.ExitCode, te.StdOut, te.StdErr,
                       te.TriggerSource, te.ScheduledForUtc, te.Reason,
                       CAST(0 AS bit) AS IsStale,
                       COALESCE(te.RequestedByUserId, br.RequestedByUserId) AS RequestedByUserId,
                       u.Username AS RequestedByUsername
                FROM TaskExecutions te
                INNER JOIN Tasks t ON te.TaskId = t.TaskId
                INNER JOIN Boxes b ON t.BoxId = b.BoxId
                LEFT JOIN Departments d ON b.DepartmentId = d.DepartmentId
                LEFT JOIN BoxRuns br ON te.BoxRunId = br.BoxRunId
                LEFT JOIN Users u ON COALESCE(te.RequestedByUserId, br.RequestedByUserId) = u.UserId
                WHERE te.Status IN ({string.Join(", ", allowedStatuses.Select((_, i) => $"@Status{i}"))})
                  AND (@BoxId IS NULL OR t.BoxId = @BoxId)
                  AND (@FromUtc IS NULL OR te.StartedAt >= @FromUtc)
                  AND (@ToUtc IS NULL OR te.StartedAt <= @ToUtc)
                  AND (@DepartmentId IS NULL OR b.DepartmentId = @DepartmentId OR b.DepartmentId IS NULL)
                  AND (@TaskName IS NULL OR t.Name LIKE '%' + @TaskName + '%')
                  AND (@TriggerSource IS NULL OR te.TriggerSource = @TriggerSource)
                ORDER BY te.StartedAt DESC";

            var parameters = new DynamicParameters();
            parameters.Add("Limit", limit);
            parameters.Add("BoxId", boxId);
            parameters.Add("FromUtc", fromUtc);
            parameters.Add("ToUtc", toUtc);
            parameters.Add("DepartmentId", departmentId);
            parameters.Add("TaskName", string.IsNullOrWhiteSpace(taskName) ? null : taskName);
            parameters.Add("TriggerSource", string.IsNullOrWhiteSpace(triggerSource) ? null : triggerSource);
            for (var i = 0; i < allowedStatuses.Length; i++)
                parameters.Add($"Status{i}", allowedStatuses[i]);

            var result = await connection.QueryAsync<ExecutionRecord>(sql, parameters);
            return result.Select(NormalizeRecord).ToList();
        }

        public async Task<Dictionary<int, string>> GetTaskStatusMapForBoxRunAsync(int boxRunId)        {
            using var connection = CreateConnection();
            const string sql = @"
                WITH LatestExecution AS (
                    SELECT TaskId, Status,
                           ROW_NUMBER() OVER (PARTITION BY TaskId ORDER BY ExecutionId DESC) AS rn
                    FROM TaskExecutions
                    WHERE BoxRunId = @BoxRunId
                )
                SELECT TaskId, Status FROM LatestExecution WHERE rn = 1";
            var rows = await connection.QueryAsync<(int TaskId, string Status)>(sql, new { BoxRunId = boxRunId });
            return rows.ToDictionary(r => r.TaskId, r => r.Status);
        }

        public async Task<int> FailRunningExecutionsForBoxRunAsync(int boxRunId, DateTime endedAtUtc, string reason)
        {
            using var connection = CreateConnection();
            const string sql = @"
                UPDATE TaskExecutions
                SET Status = 'Failed',
                    EndedAt = @EndedAtUtc,
                    Error = @Reason,
                    StdErr = @Reason
                WHERE BoxRunId = @BoxRunId
                  AND Status = 'Running'
                  AND EndedAt IS NULL;
                SELECT @@ROWCOUNT;";
            return await connection.ExecuteScalarAsync<int>(sql, new { BoxRunId = boxRunId, EndedAtUtc = endedAtUtc, Reason = reason });
        }

        public async Task<double?> GetAverageExecutionDurationSecondsAsync(int taskId, int sampleSize = 10)
        {
            if (sampleSize <= 0) sampleSize = 10;
            using var connection = CreateConnection();
            const string sql = @"
                SELECT AVG(CAST(DATEDIFF(SECOND, StartedAt, EndedAt) AS FLOAT))
                FROM (
                    SELECT TOP (@SampleSize) StartedAt, EndedAt
                    FROM TaskExecutions
                    WHERE TaskId = @TaskId
                      AND Status = 'Success'
                      AND EndedAt IS NOT NULL
                    ORDER BY StartedAt DESC
                ) AS recent";
            return await connection.ExecuteScalarAsync<double?>(sql, new { TaskId = taskId, SampleSize = sampleSize });
        }

        public async Task AddLogAsync(TaskExecutionLog log)
        {
            using var connection = CreateConnection();
            const string sql = @"
                INSERT INTO TaskExecutionLogs
                    (Id, BoxRunId, TaskId, TaskExecutionId, TimestampUtc, Level, Message, Details)
                VALUES
                    (@Id, @BoxRunId, @TaskId, @TaskExecutionId, @Timestamp, @Level, @Message, @Details);";
            await connection.ExecuteAsync(sql, new
            {
                log.Id,
                log.BoxRunId,
                log.TaskId,
                log.TaskExecutionId,
                log.Timestamp,
                log.Level,
                log.Message,
                log.Details
            });
        }

        public async Task<List<TaskExecutionLog>> GetLogsByTaskExecutionIdAsync(int taskExecutionId)
        {
            using var connection = CreateConnection();
            const string sql = @"
                SELECT Id, BoxRunId, TaskId, TaskExecutionId,
                       TimestampUtc AS Timestamp,
                       Level, Message, Details
                FROM TaskExecutionLogs
                WHERE TaskExecutionId = @TaskExecutionId
                ORDER BY TimestampUtc ASC, Id ASC";
            var rows = await connection.QueryAsync<TaskExecutionLog>(sql, new { TaskExecutionId = taskExecutionId });
            return rows.Select(NormalizeLog).ToList();
        }

        public async Task<List<TaskExecutionLog>> GetLogsByBoxRunIdAsync(int boxRunId)
        {
            using var connection = CreateConnection();
            const string sql = @"
                SELECT Id, BoxRunId, TaskId, TaskExecutionId,
                       TimestampUtc AS Timestamp,
                       Level, Message, Details
                FROM TaskExecutionLogs
                WHERE BoxRunId = @BoxRunId
                ORDER BY TimestampUtc ASC, Id ASC";
            var rows = await connection.QueryAsync<TaskExecutionLog>(sql, new { BoxRunId = boxRunId });
            return rows.Select(NormalizeLog).ToList();
        }

        public class ExecutionRecord
        {
            public int ExecutionId { get; set; }
            public int TaskId { get; set; }
            public int? BoxRunId { get; set; }   // NULL for ForceStart executions
            public int BoxId { get; set; }        // Always populated via Tasks.BoxId
            public string BoxName { get; set; } = "";
            public string BoxTimeZoneId { get; set; } = "Etc/UTC";
            public string? DepartmentName { get; set; }
            public string? FailureAlertEmail { get; set; }
            public string TaskName { get; set; } = "";
            public string TaskType { get; set; } = "";
            public string Command { get; set; } = "";
            public DateTime StartedAt { get; set; }
            public DateTime? EndedAt { get; set; }
            public string Status { get; set; } = "";
            public string Output { get; set; } = "";
            public string Error { get; set; } = "";
            public int ExitCode { get; set; }
            public string StdOut { get; set; } = "";
            public string StdErr { get; set; } = "";
            public string TriggerSource { get; set; } = TriggerSources.Scheduler;
            public DateTime? ScheduledForUtc { get; set; }
            public string? Reason { get; set; }
            public int? RequestedByUserId { get; set; }
            public string? RequestedByUsername { get; set; }
            public bool IsStale { get; set; }

            public TimeSpan? Duration =>
                EndedAt.HasValue
                    ? EndedAt.Value - StartedAt
                    : null;
        }

        private static ExecutionRecord NormalizeRecord(ExecutionRecord record)
        {
            record.StartedAt = UtcDateTimeMapper.EnsureUtc(record.StartedAt);
            record.EndedAt = UtcDateTimeMapper.EnsureUtc(record.EndedAt);
            record.ScheduledForUtc = UtcDateTimeMapper.EnsureUtc(record.ScheduledForUtc);
            record.TriggerSource = TriggerSources.Normalize(record.TriggerSource);
            return record;
        }

        private static TaskExecutionLog NormalizeLog(TaskExecutionLog log)
        {
            log.Timestamp = UtcDateTimeMapper.EnsureUtc(log.Timestamp);
            return log;
        }

        private static void ValidateTerminalStatus(string status)
        {
            if (status is not ("Success" or "Failed" or "NotExecuted" or "Aborted"))
            {
                throw new InvalidOperationException($"Invalid terminal execution status '{status}'.");
            }
        }
    }
}
