using CHRONIQ.Domain;

namespace CHRONIQ.Data;

public interface IApplicationLogRepository
{
    Task<List<ApplicationLogRecord>> GetRecentAsync(DateTime fromUtc, int limit);
    Task<ApplicationLogSummary> GetSummaryAsync(DateTime fromUtc);
}
