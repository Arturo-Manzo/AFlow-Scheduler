using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;

namespace AScheduler.Api.Services;

/// <summary>
/// Records auditable actions (Create, Update, Delete, Execute) in the AuditLog table.
/// </summary>
public interface IAuditLogService
{
    /// <summary>
    /// Writes an audit entry. Failures are swallowed and logged as warnings
    /// so that audit issues never break the main operation.
    /// </summary>
    Task LogAsync(int userId, string tableName, int recordId, string action,
                  string? oldValues = null, string? newValues = null, string? reason = null);
}

/// <summary>
/// Dapper-based implementation that writes to the AuditLog SQL table.
/// </summary>
public class AuditLogService : IAuditLogService
{
    private readonly string _connectionString;
    private readonly ILogger<AuditLogService> _logger;

    public AuditLogService(IConfiguration configuration, ILogger<AuditLogService> logger)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(logger);

        _connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("Missing 'Default' connection string in configuration.");
        _logger = logger;
    }

    public async Task LogAsync(int userId, string tableName, int recordId, string action,
                               string? oldValues = null, string? newValues = null, string? reason = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tableName);
        ArgumentException.ThrowIfNullOrWhiteSpace(action);

        try
        {
            using IDbConnection connection = new SqlConnection(_connectionString);
            const string sql = @"
                INSERT INTO AuditLog (UserId, TableName, RecordId, Action, OldValues, NewValues, Reason, CreatedAt)
                VALUES (@UserId, @TableName, @RecordId, @Action, @OldValues, @NewValues, @Reason, GETDATE())";

            await connection.ExecuteAsync(sql, new
            {
                UserId    = userId,
                TableName = tableName,
                RecordId  = recordId,
                Action    = action,
                OldValues = oldValues,
                NewValues = newValues,
                Reason    = reason
            });
        }
        catch (Exception ex)
        {
            // Audit failures must not disrupt the main operation.
            _logger.LogWarning(ex, "Audit log write failed for {Action} on {Table}[{Id}].", action, tableName, recordId);
        }
    }
}
