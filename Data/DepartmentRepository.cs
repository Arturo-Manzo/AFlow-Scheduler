using System.Data;
using Microsoft.Data.SqlClient;
using Dapper;
using Microsoft.Extensions.Configuration;
using AScheduler.Domain;

namespace AScheduler.Data
{
    /// <summary>
    /// Repository implementation for department management using Dapper ORM.
    /// </summary>
    public class DepartmentRepository : IDepartmentRepository
    {
        private readonly string _connectionString;
        private const string RetryPolicyProjectionSql = @"
                CASE
                    WHEN TRY_CONVERT(INT, RetryPolicy) IS NOT NULL THEN TRY_CONVERT(INT, RetryPolicy)
                    WHEN LOWER(LTRIM(RTRIM(RetryPolicy))) IN ('require-approval', 'requireapproval') THEN 0
                    WHEN LOWER(LTRIM(RTRIM(RetryPolicy))) = 'auto' THEN 1
                    WHEN LOWER(LTRIM(RTRIM(RetryPolicy))) IN ('manual-only', 'manualonly') THEN 2
                    ELSE 0
                END";

        public DepartmentRepository(IConfiguration config)
        {
            ArgumentNullException.ThrowIfNull(config);
            _connectionString = config.GetConnectionString("Default")!;
        }

        private IDbConnection CreateConnection() => new SqlConnection(_connectionString);

        /// <summary>
        /// Retrieves all departments.
        /// </summary>
        public async Task<List<Department>> GetAllAsync()
        {
            using var connection = CreateConnection();
            var sql = $@"
                SELECT 
                    DepartmentId,
                    Name,
                    Description,
                    ContactEmail,
                    {RetryPolicyProjectionSql} AS RetryPolicy,
                    LogRetentionDays,
                    CreatedAt,
                    UpdatedAt
                FROM Departments
                ORDER BY Name";
            
            var result = await connection.QueryAsync<Department>(sql);
            return result.ToList();
        }

        /// <summary>
        /// Retrieves a department by ID.
        /// </summary>
        public async Task<Department?> GetByIdAsync(int departmentId)
        {
            using var connection = CreateConnection();
            var sql = $@"
                SELECT 
                    DepartmentId,
                    Name,
                    Description,
                    ContactEmail,
                    {RetryPolicyProjectionSql} AS RetryPolicy,
                    LogRetentionDays,
                    CreatedAt,
                    UpdatedAt
                FROM Departments
                WHERE DepartmentId = @DepartmentId";
            
            var result = await connection.QueryFirstOrDefaultAsync<Department>(sql, new { DepartmentId = departmentId });
            return result;
        }

        /// <summary>
        /// Retrieves a department by name.
        /// </summary>
        public async Task<Department?> GetByNameAsync(string name)
        {
            using var connection = CreateConnection();
            var sql = $@"
                SELECT 
                    DepartmentId,
                    Name,
                    Description,
                    ContactEmail,
                    {RetryPolicyProjectionSql} AS RetryPolicy,
                    LogRetentionDays,
                    CreatedAt,
                    UpdatedAt
                FROM Departments
                WHERE Name = @Name";
            
            var result = await connection.QueryFirstOrDefaultAsync<Department>(sql, new { Name = name });
            return result;
        }

        /// <summary>
        /// Creates a new department.
        /// </summary>
        public async Task<int> CreateAsync(string name, string? description, string contactEmail, int retryPolicy, int logRetentionDays)
        {
            using var connection = CreateConnection();
            const string sql = @"
            INSERT INTO Departments (Name, Description, ContactEmail, RetryPolicy, LogRetentionDays, CreatedAt, UpdatedAt)
            VALUES (@Name, @Description, @ContactEmail, @RetryPolicy, @LogRetentionDays, GETUTCDATE(), GETUTCDATE());
                SELECT CAST(SCOPE_IDENTITY() as int);";
            
            var departmentId = await connection.QuerySingleAsync<int>(sql, new
            {
                Name = name,
                Description = description,
                ContactEmail = contactEmail,
                RetryPolicy = retryPolicy,
                LogRetentionDays = logRetentionDays
            });
            
            return departmentId;
        }

        /// <summary>
        /// Updates an existing department.
        /// </summary>
        public async Task<bool> UpdateAsync(int departmentId, string name, string? description, string contactEmail, int retryPolicy, int logRetentionDays)
        {
            using var connection = CreateConnection();
            const string sql = @"
                UPDATE Departments
                SET Name = @Name,
                    Description = @Description,
                    ContactEmail = @ContactEmail,
                    RetryPolicy = @RetryPolicy,
                    LogRetentionDays = @LogRetentionDays,
                    UpdatedAt = GETUTCDATE()
                WHERE DepartmentId = @DepartmentId";
            
            var affectedRows = await connection.ExecuteAsync(sql, new
            {
                DepartmentId = departmentId,
                Name = name,
                Description = description,
                ContactEmail = contactEmail,
                RetryPolicy = retryPolicy,
                LogRetentionDays = logRetentionDays
            });
            
            return affectedRows > 0;
        }

        /// <summary>
        /// Deletes a department.
        /// Note: Soft delete is not implemented; this performs a physical delete.
        /// Ensure no foreign key references exist before deletion.
        /// </summary>
        public async Task<bool> DeleteAsync(int departmentId)
        {
            using var connection = CreateConnection();
            const string sql = @"
                DELETE FROM Departments
                WHERE DepartmentId = @DepartmentId";
            
            var affectedRows = await connection.ExecuteAsync(sql, new { DepartmentId = departmentId });
            return affectedRows > 0;
        }

        /// <summary>
        /// Retrieves the retry policy for a department.
        /// </summary>
        public async Task<int?> GetRetryPolicyAsync(int departmentId)
        {
            using var connection = CreateConnection();
            var sql = $@"
                SELECT {RetryPolicyProjectionSql}
                FROM Departments
                WHERE DepartmentId = @DepartmentId";
            
            var result = await connection.QueryFirstOrDefaultAsync<int?>(sql, new { DepartmentId = departmentId });
            return result;
        }

        /// <summary>
        /// Gets all boxes assigned to a department.
        /// </summary>
        public async Task<List<BoxDefinition>> GetDepartmentBoxesAsync(int departmentId)
        {
            using var connection = CreateConnection();
            const string sql = @"
                SELECT 
                    BoxId AS Id,
                    Name,
                    Description,
                    CronExpression,
                    TimeZoneId,
                    AllowParallel,
                    Enabled,
                    CreatedAtUtc,
                    LastRunUtc,
                    NotificationEmail,
                    DepartmentId
                FROM Boxes
                WHERE DepartmentId = @DepartmentId
                ORDER BY Name";
            
            var result = await connection.QueryAsync<BoxDefinition>(sql, new { DepartmentId = departmentId });
            return result.ToList();
        }

        /// <summary>
        /// Gets all users assigned to a department.
        /// </summary>
        public async Task<List<string>> GetDepartmentUsersAsync(int departmentId)
        {
            using var connection = CreateConnection();
            const string sql = @"
                SELECT Username
                FROM Users
                WHERE DepartmentId = @DepartmentId
                ORDER BY Username";
            
            var result = await connection.QueryAsync<string>(sql, new { DepartmentId = departmentId });
            return result.ToList();
        }

        /// <summary>
        /// Gets the "Default" department (always exists).
        /// </summary>
        public async Task<Department?> GetDefaultDepartmentAsync()
        {
            using var connection = CreateConnection();
            var sql = $@"
                SELECT 
                    DepartmentId,
                    Name,
                    Description,
                    ContactEmail,
                    {RetryPolicyProjectionSql} AS RetryPolicy,
                    LogRetentionDays,
                    CreatedAt,
                    UpdatedAt
                FROM Departments
                WHERE Name = 'Default'";
            
            var result = await connection.QueryFirstOrDefaultAsync<Department>(sql);
            return result;
        }
    }
}
