using System.Data;
using Microsoft.Data.SqlClient;
using Dapper;
using Microsoft.Extensions.Configuration;
using AScheduler.Domain;

namespace AScheduler.Data
{
    public class TaskRepository : ITaskRepository
    {
        private readonly string _connectionString;

        public TaskRepository(IConfiguration config)
        {
            ArgumentNullException.ThrowIfNull(config);
            _connectionString = config.GetConnectionString("Default")!;
        }

        private IDbConnection CreateConnection() => new SqlConnection(_connectionString);

        public async Task<List<TaskDefinition>> GetTasksForBoxAsync(int boxId)
        {
            using var connection = CreateConnection();
            const string sql = @"
                SELECT TaskId AS Id, BoxId, Name, Description, Command, TaskType, AllowParallel, SortOrder, Enabled, CreatedAtUtc
                FROM Tasks WHERE BoxId = @BoxId AND Enabled = 1
                ORDER BY TaskId";
            var result = await connection.QueryAsync<TaskDto>(sql, new { BoxId = boxId });
            return result.Select(MapToDefinition).ToList();
        }

        public async Task<TaskDefinition?> GetByIdAsync(int taskId)
        {
            using var connection = CreateConnection();
            const string sql = @"
                SELECT TaskId AS Id, BoxId, Name, Description, Command, TaskType, AllowParallel, SortOrder, Enabled, CreatedAtUtc
                FROM Tasks WHERE TaskId = @TaskId";
            var dto = await connection.QueryFirstOrDefaultAsync<TaskDto>(sql, new { TaskId = taskId });
            return dto == null ? null : MapToDefinition(dto);
        }

        public async Task<List<TaskSearchResult>> SearchAsync(string query, int limit)
        {
            using var connection = CreateConnection();

            var normalizedQuery = (query ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(normalizedQuery))
                return new List<TaskSearchResult>();

            var safeLimit = Math.Max(1, Math.Min(limit, 100));
            var like = $"%{normalizedQuery}%";
            var startsWith = $"{normalizedQuery}%";

            const string sql = @"
                SELECT TOP (@Limit)
                    t.TaskId,
                    t.BoxId,
                    t.Name AS TaskName,
                    t.Description AS TaskDescription,
                    t.Command,
                    t.TaskType,
                    t.Enabled AS TaskEnabled,
                    t.CreatedAtUtc,
                    b.Name AS BoxName,
                    b.Description AS BoxDescription,
                    b.Enabled AS BoxEnabled
                FROM Tasks t
                INNER JOIN Boxes b ON b.BoxId = t.BoxId
                WHERE t.Name LIKE @Like
                   OR t.Description LIKE @Like
                   OR t.Command LIKE @Like
                   OR b.Name LIKE @Like
                   OR b.Description LIKE @Like
                ORDER BY
                    CASE
                        WHEN t.Name LIKE @StartsWith THEN 0
                        WHEN b.Name LIKE @StartsWith THEN 1
                        WHEN t.Command LIKE @StartsWith THEN 2
                        ELSE 3
                    END,
                    t.Enabled DESC,
                    b.Enabled DESC,
                    t.Name,
                    b.Name";

            var results = await connection.QueryAsync<TaskSearchResult>(sql, new
            {
                Limit = safeLimit,
                Like = like,
                StartsWith = startsWith
            });

            return results
                .Select(result =>
                {
                    result.CreatedAtUtc = UtcDateTimeMapper.EnsureUtc(result.CreatedAtUtc);
                    return result;
                })
                .ToList();
        }

        public async Task<int> CreateAsync(int boxId, string name, string description, string command, string taskType)
        {
            using var connection = CreateConnection();
            const string sql = @"
                INSERT INTO Tasks (BoxId, Name, Description, Command, TaskType, AllowParallel, SortOrder, Enabled, CreatedAtUtc)
                VALUES (@BoxId, @Name, @Description, @Command, @TaskType, @AllowParallel, @SortOrder, 1, SYSUTCDATETIME());
                SELECT CAST(SCOPE_IDENTITY() AS INT);";
            return await connection.ExecuteScalarAsync<int>(sql,
                new { BoxId = boxId, Name = name, Description = description, Command = command, TaskType = taskType, AllowParallel = false, SortOrder = 0 });
        }

        public async Task<bool> UpdateAsync(int taskId, string name, string description, string command, string taskType, bool enabled)
        {
            using var connection = CreateConnection();
            const string sql = @"
                UPDATE Tasks
                SET Name = @Name, Description = @Description, Command = @Command,
                    TaskType = @TaskType, AllowParallel = @AllowParallel, SortOrder = @SortOrder, Enabled = @Enabled
                WHERE TaskId = @TaskId";
            var rows = await connection.ExecuteAsync(sql,
                new { TaskId = taskId, Name = name, Description = description, Command = command, TaskType = taskType, AllowParallel = false, SortOrder = 0, Enabled = enabled });
            return rows > 0;
        }

        public async Task<bool> DeleteAsync(int taskId)
        {
            using var connection = CreateConnection();
            const string sql = "UPDATE Tasks SET Enabled = 0 WHERE TaskId = @TaskId";
            var rows = await connection.ExecuteAsync(sql, new { TaskId = taskId });
            return rows > 0;
        }

        public async Task<List<int>> GetTaskDependenciesAsync(int taskId)
        {
            using var connection = CreateConnection();
            const string sql = "SELECT DependsOnTaskId FROM TaskDependencies WHERE TaskId = @TaskId";
            var result = await connection.QueryAsync<int>(sql, new { TaskId = taskId });
            return result.ToList();
        }

        public async Task ReplaceTaskDependenciesAsync(int taskId, IEnumerable<int> dependencyTaskIds)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();
            using var transaction = connection.BeginTransaction();

            await connection.ExecuteAsync("DELETE FROM TaskDependencies WHERE TaskId = @TaskId", new { TaskId = taskId }, transaction);

            var distinctIds = dependencyTaskIds.Distinct().Where(id => id > 0)
                .Select(id => new { TaskId = taskId, DependsOnTaskId = id }).ToList();

            if (distinctIds.Count > 0)
                await connection.ExecuteAsync("INSERT INTO TaskDependencies (TaskId, DependsOnTaskId) VALUES (@TaskId, @DependsOnTaskId)", distinctIds, transaction);

            transaction.Commit();
        }

        private static TaskDefinition MapToDefinition(TaskDto dto)
        {
            if (!Enum.TryParse<TaskType>(dto.TaskType, true, out var taskType))
                throw new InvalidOperationException($"Invalid TaskType: {dto.TaskType}");
            return new TaskDefinition
            {
                Id = dto.Id, BoxId = dto.BoxId, Name = dto.Name, Description = dto.Description,
                Command = dto.Command, TaskType = taskType, AllowParallel = dto.AllowParallel,
                SortOrder = dto.SortOrder, Enabled = dto.Enabled, CreatedAtUtc = UtcDateTimeMapper.EnsureUtc(dto.CreatedAtUtc)
            };
        }

        private class TaskDto
        {
            public int Id { get; set; }
            public int BoxId { get; set; }
            public string Name { get; set; } = "";
            public string Description { get; set; } = "";
            public string Command { get; set; } = "";
            public string TaskType { get; set; } = "";
            public bool AllowParallel { get; set; }
            public int SortOrder { get; set; }
            public bool Enabled { get; set; }
            public DateTime CreatedAtUtc { get; set; }
        }
    }
}
