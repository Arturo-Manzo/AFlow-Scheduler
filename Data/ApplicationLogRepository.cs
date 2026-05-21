using System.Data;
using CHRONIQ.Domain;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace CHRONIQ.Data;

public sealed class ApplicationLogRepository : IApplicationLogRepository
{
    private static readonly string[] IncludedLevels = ["Error", "Fatal", "Warning"];
    private readonly string _connectionString;

    public ApplicationLogRepository(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        _connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("Missing 'Default' connection string in configuration.");
    }

    private IDbConnection CreateConnection() => new SqlConnection(_connectionString);

    public async Task<List<ApplicationLogRecord>> GetRecentAsync(DateTime fromUtc, int limit)
    {
        using var connection = CreateConnection();
        const string sql = @"
            SELECT TOP (@Limit)
                Id,
                LogFileName,
                Timestamp,
                Level,
                Message,
                ErrorFile,
                ErrorMethod,
                ErrorLine,
                ExceptionType,
                Source,
                CorrelationId,
                UserId,
                RequestPath,
                StatusCode,
                CreatedAt
            FROM dbo.ApplicationLogs
            WHERE Timestamp >= @FromUtc
              AND Level IN @Levels
            ORDER BY Timestamp DESC, Id DESC;";

        var rows = await connection.QueryAsync<ApplicationLogRecord>(sql, new
        {
            FromUtc = fromUtc,
            Limit = Math.Clamp(limit, 1, 200),
            Levels = IncludedLevels
        });

        return rows.Select(Normalize).ToList();
    }

    public async Task<ApplicationLogSummary> GetSummaryAsync(DateTime fromUtc)
    {
        using var connection = CreateConnection();
        const string sql = @"
            SELECT
                SUM(CASE WHEN Level IN ('Error', 'Fatal') THEN 1 ELSE 0 END) AS ErrorCount,
                SUM(CASE WHEN Level = 'Warning' THEN 1 ELSE 0 END) AS WarningCount
            FROM dbo.ApplicationLogs
            WHERE Timestamp >= @FromUtc
              AND Level IN @Levels;";

        return await connection.QuerySingleAsync<ApplicationLogSummary>(sql, new
        {
            FromUtc = fromUtc,
            Levels = IncludedLevels
        });
    }

    private static ApplicationLogRecord Normalize(ApplicationLogRecord record)
    {
        record.Timestamp = UtcDateTimeMapper.EnsureUtc(record.Timestamp);
        record.CreatedAt = UtcDateTimeMapper.EnsureUtc(record.CreatedAt);
        return record;
    }
}
