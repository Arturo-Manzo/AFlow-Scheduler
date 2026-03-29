using System.Data;
using Microsoft.Data.SqlClient;
using Dapper;
using Microsoft.Extensions.Configuration;
using AScheduler.Domain;

namespace AScheduler.Data
{
    public class BoxRepository : IBoxRepository
    {
        private readonly string _connectionString;

        public BoxRepository(IConfiguration config)
        {
            ArgumentNullException.ThrowIfNull(config);
            _connectionString = config.GetConnectionString("Default")!;
        }

        private IDbConnection CreateConnection() => new SqlConnection(_connectionString);

        public async Task<List<BoxDefinition>> GetActiveBoxesAsync()
        {
            using var connection = CreateConnection();
            const string sql = @"
                SELECT BoxId AS Id, Name, Description, CronExpression, AllowParallel, Enabled, CreatedAtUtc, LastRunUtc
                FROM Boxes WHERE Enabled = 1";
            var result = await connection.QueryAsync<BoxDefinition>(sql);
            return result.ToList();
        }

        public async Task<BoxDefinition?> GetByIdAsync(int boxId)
        {
            using var connection = CreateConnection();
            const string sql = @"
                SELECT BoxId AS Id, Name, Description, CronExpression, AllowParallel, Enabled, CreatedAtUtc, LastRunUtc
                FROM Boxes WHERE BoxId = @BoxId";
            return await connection.QueryFirstOrDefaultAsync<BoxDefinition>(sql, new { BoxId = boxId });
        }

        public async Task<int> CreateAsync(string name, string description, string cronExpression, bool allowParallel)
        {
            using var connection = CreateConnection();
            const string sql = @"
                INSERT INTO Boxes (Name, Description, CronExpression, AllowParallel, Enabled, CreatedAtUtc)
                VALUES (@Name, @Description, @CronExpression, @AllowParallel, 1, SYSUTCDATETIME());
                SELECT CAST(SCOPE_IDENTITY() AS INT);";
            return await connection.ExecuteScalarAsync<int>(sql,
                new { Name = name, Description = description, CronExpression = cronExpression, AllowParallel = allowParallel });
        }

        public async Task<bool> UpdateAsync(int boxId, string name, string description, string cronExpression, bool allowParallel, bool enabled)
        {
            using var connection = CreateConnection();
            const string sql = @"
                UPDATE Boxes
                SET Name = @Name, Description = @Description, CronExpression = @CronExpression,
                    AllowParallel = @AllowParallel, Enabled = @Enabled
                WHERE BoxId = @BoxId";
            var rows = await connection.ExecuteAsync(sql,
                new { BoxId = boxId, Name = name, Description = description, CronExpression = cronExpression, AllowParallel = allowParallel, Enabled = enabled });
            return rows > 0;
        }

        public async Task<bool> DeleteAsync(int boxId)
        {
            using var connection = CreateConnection();
            const string sql = "UPDATE Boxes SET Enabled = 0 WHERE BoxId = @BoxId";
            var rows = await connection.ExecuteAsync(sql, new { BoxId = boxId });
            return rows > 0;
        }

        public async Task UpdateLastRunAsync(int boxId, DateTime lastRunUtc)
        {
            using var connection = CreateConnection();
            const string sql = "UPDATE Boxes SET LastRunUtc = @LastRunUtc WHERE BoxId = @BoxId";
            await connection.ExecuteAsync(sql, new { BoxId = boxId, LastRunUtc = lastRunUtc });
        }

        // --- BoxRun ---

        public async Task<int> CreateBoxRunAsync(int boxId, DateTime? scheduledForUtc, string triggerSource, int? requestedByUserId)
        {
            using var connection = CreateConnection();
            const string sql = @"
                INSERT INTO BoxRuns (BoxId, ScheduledForUtc, Status, TriggerSource, RequestedByUserId, CreatedAtUtc)
                VALUES (@BoxId, @ScheduledForUtc, 'Pending', @TriggerSource, @RequestedByUserId, SYSUTCDATETIME());
                SELECT CAST(SCOPE_IDENTITY() AS INT);";
            return await connection.ExecuteScalarAsync<int>(sql,
                new { BoxId = boxId, ScheduledForUtc = scheduledForUtc, TriggerSource = triggerSource, RequestedByUserId = requestedByUserId });
        }

        public async Task UpdateBoxRunStatusAsync(int boxRunId, string status, DateTime? startedAtUtc = null, DateTime? endedAtUtc = null)
        {
            using var connection = CreateConnection();
            const string sql = @"
                UPDATE BoxRuns
                SET Status       = @Status,
                    StartedAtUtc = COALESCE(@StartedAtUtc, StartedAtUtc),
                    EndedAtUtc   = COALESCE(@EndedAtUtc, EndedAtUtc)
                WHERE BoxRunId = @BoxRunId";
            await connection.ExecuteAsync(sql, new { BoxRunId = boxRunId, Status = status, StartedAtUtc = startedAtUtc, EndedAtUtc = endedAtUtc });
        }

        public async Task<bool> HasBoxRunForScheduledTimeAsync(int boxId, DateTime scheduledForUtc)
        {
            using var connection = CreateConnection();
            const string sql = @"
                SELECT COUNT(1) FROM BoxRuns
                WHERE BoxId = @BoxId AND ScheduledForUtc = @ScheduledForUtc AND Status <> 'Pending'";
            var count = await connection.ExecuteScalarAsync<int>(sql, new { BoxId = boxId, ScheduledForUtc = scheduledForUtc });
            return count > 0;
        }

        // --- BoxExecutionQueue ---

        public async Task<List<BoxQueueItem>> GetPendingQueueItemsAsync()
        {
            using var connection = CreateConnection();
            const string sql = @"
                SELECT QueueId, BoxId, RequestedByUserId, IgnoreDependencies, IgnoreSchedule, Reason, Status, CreatedAt
                FROM BoxExecutionQueue
                WHERE Status = 'Pending'
                ORDER BY CreatedAt ASC";
            var result = await connection.QueryAsync<BoxQueueItem>(sql);
            return result.ToList();
        }

        public async Task MarkQueueItemAsync(int queueId, string status)
        {
            using var connection = CreateConnection();
            const string sql = @"
                UPDATE BoxExecutionQueue SET Status = @Status, ProcessedAt = @ProcessedAt
                WHERE QueueId = @QueueId";
            await connection.ExecuteAsync(sql, new { QueueId = queueId, Status = status, ProcessedAt = DateTime.UtcNow });
        }

        public async Task<int> InsertQueueItemAsync(int boxId, int? userId, bool ignoreDependencies, bool ignoreSchedule, string? reason)
        {
            using var connection = CreateConnection();
            const string sql = @"
                INSERT INTO BoxExecutionQueue (BoxId, RequestedByUserId, IgnoreDependencies, IgnoreSchedule, Reason, Status, CreatedAt)
                VALUES (@BoxId, @UserId, @IgnoreDependencies, @IgnoreSchedule, @Reason, 'Pending', SYSUTCDATETIME());
                SELECT CAST(SCOPE_IDENTITY() AS INT);";
            return await connection.ExecuteScalarAsync<int>(sql,
                new { BoxId = boxId, UserId = userId, IgnoreDependencies = ignoreDependencies, IgnoreSchedule = ignoreSchedule, Reason = reason });
        }
    }
}
