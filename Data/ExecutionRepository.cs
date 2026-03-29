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
                SELECT te.ExecutionId, te.TaskId, te.BoxRunId, br.BoxId, b.Name AS BoxName,
                       t.Name AS TaskName, te.StartedAt, te.EndedAt, te.Status,
                       te.Output, te.Error, te.ExitCode, te.StdOut, te.StdErr,
                       te.TriggerSource, te.ScheduledForUtc
                FROM TaskExecutions te
                INNER JOIN Tasks t ON te.TaskId = t.TaskId
                INNER JOIN BoxRuns br ON te.BoxRunId = br.BoxRunId
                INNER JOIN Boxes b ON br.BoxId = b.BoxId
                WHERE te.BoxRunId = @BoxRunId
                ORDER BY te.StartedAt ASC";
            var result = await connection.QueryAsync<ExecutionRecord>(sql, new { BoxRunId = boxRunId });
            return result.ToList();
        }

        public async Task<List<ExecutionRecord>> GetExecutionsForTaskAsync(int taskId)
        {
            using var connection = CreateConnection();
            const string sql = @"
                SELECT te.ExecutionId, te.TaskId, te.BoxRunId, br.BoxId, b.Name AS BoxName,
                       t.Name AS TaskName, te.StartedAt, te.EndedAt, te.Status,
                       te.Output, te.Error, te.ExitCode, te.StdOut, te.StdErr,
                       te.TriggerSource, te.ScheduledForUtc
                FROM TaskExecutions te
                INNER JOIN Tasks t ON te.TaskId = t.TaskId
                INNER JOIN BoxRuns br ON te.BoxRunId = br.BoxRunId
                INNER JOIN Boxes b ON br.BoxId = b.BoxId
                WHERE te.TaskId = @TaskId
                ORDER BY te.StartedAt DESC";
            var result = await connection.QueryAsync<ExecutionRecord>(sql, new { TaskId = taskId });
            return result.ToList();
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
                SELECT TOP 1 te.ExecutionId, te.TaskId, te.BoxRunId, br.BoxId, b.Name AS BoxName,
                       t.Name AS TaskName, te.StartedAt, te.EndedAt, te.Status,
                       te.Output, te.Error, te.ExitCode, te.StdOut, te.StdErr,
                       te.TriggerSource, te.ScheduledForUtc
                FROM TaskExecutions te
                INNER JOIN Tasks t ON te.TaskId = t.TaskId
                INNER JOIN BoxRuns br ON te.BoxRunId = br.BoxRunId
                INNER JOIN Boxes b ON br.BoxId = b.BoxId
                WHERE te.TaskId = @TaskId AND te.BoxRunId = @BoxRunId
                ORDER BY te.EndedAt DESC, te.StartedAt DESC";
            return await connection.QueryFirstOrDefaultAsync<ExecutionRecord>(sql, new { TaskId = taskId, BoxRunId = boxRunId });
        }

        public async Task<List<ExecutionRecord>> GetLatestExecutionsAsync(int limit = 20)
        {
            if (limit <= 0) limit = 20;
            using var connection = CreateConnection();
            const string sql = @"
                SELECT TOP (@Limit)
                       te.ExecutionId, te.TaskId, te.BoxRunId, br.BoxId, b.Name AS BoxName,
                       t.Name AS TaskName, te.StartedAt, te.EndedAt, te.Status,
                       te.Output, te.Error, te.ExitCode, te.StdOut, te.StdErr,
                       te.TriggerSource, te.ScheduledForUtc
                FROM TaskExecutions te
                INNER JOIN Tasks t ON te.TaskId = t.TaskId
                INNER JOIN BoxRuns br ON te.BoxRunId = br.BoxRunId
                INNER JOIN Boxes b ON br.BoxId = b.BoxId
                ORDER BY te.StartedAt DESC";
            var result = await connection.QueryAsync<ExecutionRecord>(sql, new { Limit = limit });
            return result.ToList();
        }

        public class ExecutionRecord
        {
            public int ExecutionId { get; set; }
            public int TaskId { get; set; }
            public int BoxRunId { get; set; }
            public int BoxId { get; set; }
            public string BoxName { get; set; } = "";
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
        }
    }
}
