using System.Data;
using Microsoft.Data.SqlClient;
using Dapper;
using Microsoft.Extensions.Configuration;

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
                       t.Name AS TaskName, te.StartedAt, te.EndedAt, te.Status,
                       te.Output, te.Error, te.ExitCode, te.StdOut, te.StdErr,
                       te.TriggerSource, te.ScheduledForUtc, te.Reason,
                       COALESCE(te.RequestedByUserId, br.RequestedByUserId) AS RequestedByUserId,
                       u.Username AS RequestedByUsername
                FROM TaskExecutions te
                INNER JOIN Tasks t ON te.TaskId = t.TaskId
                INNER JOIN Boxes b ON t.BoxId = b.BoxId
                INNER JOIN BoxRuns br ON te.BoxRunId = br.BoxRunId
                LEFT JOIN Users u ON COALESCE(te.RequestedByUserId, br.RequestedByUserId) = u.UserId
                WHERE te.BoxRunId = @BoxRunId
                ORDER BY te.StartedAt ASC";
            var result = await connection.QueryAsync<ExecutionRecord>(sql, new { BoxRunId = boxRunId });
            return result.Select(NormalizeRecord).ToList();
        }

        public async Task<List<ExecutionRecord>> GetExecutionsForTaskAsync(int taskId)
        {
            using var connection = CreateConnection();
            const string sql = @"
                SELECT te.ExecutionId, te.TaskId, te.BoxRunId, t.BoxId, b.Name AS BoxName,
                       b.TimeZoneId AS BoxTimeZoneId,
                       t.Name AS TaskName, te.StartedAt, te.EndedAt, te.Status,
                       te.Output, te.Error, te.ExitCode, te.StdOut, te.StdErr,
                       te.TriggerSource, te.ScheduledForUtc, te.Reason,
                       COALESCE(te.RequestedByUserId, br.RequestedByUserId) AS RequestedByUserId,
                       u.Username AS RequestedByUsername
                FROM TaskExecutions te
                INNER JOIN Tasks t ON te.TaskId = t.TaskId
                INNER JOIN Boxes b ON t.BoxId = b.BoxId
                LEFT JOIN BoxRuns br ON te.BoxRunId = br.BoxRunId
                LEFT JOIN Users u ON COALESCE(te.RequestedByUserId, br.RequestedByUserId) = u.UserId
                WHERE te.TaskId = @TaskId
                ORDER BY te.StartedAt DESC";
            var result = await connection.QueryAsync<ExecutionRecord>(sql, new { TaskId = taskId });
            return result.Select(NormalizeRecord).ToList();
        }

        public async Task SaveExecutionAsync(int taskId, int boxRunId, DateTime startedAt, DateTime endedAt,
            string status, string output, string error, int exitCode,
            string stdOut, string stdErr, string triggerSource, DateTime? scheduledForUtc)
        {
            using var connection = CreateConnection();
            const string sql = @"
                INSERT INTO TaskExecutions
                    (TaskId, BoxRunId, StartedAt, EndedAt, Status, Output, Error, ExitCode,
                     StdOut, StdErr, TriggerSource, ScheduledForUtc)
                VALUES
                    (@TaskId, @BoxRunId, @StartedAt, @EndedAt, @Status, @Output, @Error,
                     @ExitCode, @StdOut, @StdErr, @TriggerSource, @ScheduledForUtc)";
            await connection.ExecuteAsync(sql, new
            {
                TaskId = taskId, BoxRunId = boxRunId, StartedAt = startedAt, EndedAt = endedAt,
                Status = status, Output = output, Error = error, ExitCode = exitCode,
                StdOut = stdOut, StdErr = stdErr, TriggerSource = triggerSource, ScheduledForUtc = scheduledForUtc
            });
        }

        public async Task<ExecutionRecord?> GetLastExecutionForTaskInBoxRunAsync(int taskId, int boxRunId)
        {
            using var connection = CreateConnection();
            const string sql = @"
                SELECT TOP 1 te.ExecutionId, te.TaskId, te.BoxRunId, t.BoxId, b.Name AS BoxName,
                       b.TimeZoneId AS BoxTimeZoneId,
                       t.Name AS TaskName, te.StartedAt, te.EndedAt, te.Status,
                       te.Output, te.Error, te.ExitCode, te.StdOut, te.StdErr,
                       te.TriggerSource, te.ScheduledForUtc, te.Reason,
                       COALESCE(te.RequestedByUserId, br.RequestedByUserId) AS RequestedByUserId,
                       u.Username AS RequestedByUsername
                FROM TaskExecutions te
                INNER JOIN Tasks t ON te.TaskId = t.TaskId
                INNER JOIN Boxes b ON t.BoxId = b.BoxId
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
                       t.Name AS TaskName, te.StartedAt, te.EndedAt, te.Status,
                       te.Output, te.Error, te.ExitCode, te.StdOut, te.StdErr,
                       te.TriggerSource, te.ScheduledForUtc, te.Reason,
                       COALESCE(te.RequestedByUserId, br.RequestedByUserId) AS RequestedByUserId,
                       u.Username AS RequestedByUsername
                FROM TaskExecutions te
                INNER JOIN Tasks t ON te.TaskId = t.TaskId
                INNER JOIN Boxes b ON t.BoxId = b.BoxId
                LEFT JOIN BoxRuns br ON te.BoxRunId = br.BoxRunId
                LEFT JOIN Users u ON COALESCE(te.RequestedByUserId, br.RequestedByUserId) = u.UserId
                ORDER BY te.StartedAt DESC";
            var result = await connection.QueryAsync<ExecutionRecord>(sql, new { Limit = limit });
            return result.Select(NormalizeRecord).ToList();
        }

        public async Task SaveDirectExecutionAsync(int taskId, DateTime startedAt, DateTime endedAt,
            string status, string output, string error, int exitCode, string stdOut, string stdErr,
            int? requestedByUserId, string reason)
        {
            using var connection = CreateConnection();
            // BoxRunId is stored as NULL — this record belongs to an isolated TaskForceStart,
            // not to any BoxRun. INNER JOIN-based history queries intentionally exclude these.
            const string sql = @"
                INSERT INTO TaskExecutions
                    (TaskId, BoxRunId, StartedAt, EndedAt, Status, Output, Error, ExitCode,
                     StdOut, StdErr, TriggerSource, ScheduledForUtc, RequestedByUserId, Reason)
                VALUES
                    (@TaskId, NULL, @StartedAt, @EndedAt, @Status, @Output, @Error,
                     @ExitCode, @StdOut, @StdErr, 'ForceStart', NULL, @RequestedByUserId, @Reason)";
            await connection.ExecuteAsync(sql, new
            {
                TaskId = taskId, StartedAt = startedAt, EndedAt = endedAt,
                Status = status, Output = output, Error = error, ExitCode = exitCode,
                StdOut = stdOut, StdErr = stdErr, RequestedByUserId = requestedByUserId, Reason = reason
            });
        }

        public class ExecutionRecord
        {
            public int ExecutionId { get; set; }
            public int TaskId { get; set; }
            public int? BoxRunId { get; set; }   // NULL for ForceStart executions
            public int BoxId { get; set; }        // Always populated via Tasks.BoxId
            public string BoxName { get; set; } = "";
            public string BoxTimeZoneId { get; set; } = "Etc/UTC";
            public string TaskName { get; set; } = "";
            public DateTime StartedAt { get; set; }
            public DateTime? EndedAt { get; set; }
            public string Status { get; set; } = "";
            public string Output { get; set; } = "";
            public string Error { get; set; } = "";
            public int ExitCode { get; set; }
            public string StdOut { get; set; } = "";
            public string StdErr { get; set; } = "";
            public string TriggerSource { get; set; } = "Scheduled";
            public DateTime? ScheduledForUtc { get; set; }
            public string? Reason { get; set; }
            public int? RequestedByUserId { get; set; }
            public string? RequestedByUsername { get; set; }
        }

        private static ExecutionRecord NormalizeRecord(ExecutionRecord record)
        {
            record.StartedAt = UtcDateTimeMapper.EnsureUtc(record.StartedAt);
            record.EndedAt = UtcDateTimeMapper.EnsureUtc(record.EndedAt);
            record.ScheduledForUtc = UtcDateTimeMapper.EnsureUtc(record.ScheduledForUtc);
            return record;
        }
    }
}
