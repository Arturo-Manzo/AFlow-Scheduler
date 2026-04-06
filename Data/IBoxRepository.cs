using CHRONIQ.Domain;

namespace CHRONIQ.Data
{
    public interface IBoxRepository
    {
        Task<List<BoxDefinition>> GetActiveBoxesAsync();
        Task<List<BoxDefinition>> GetActiveBoxesByDepartmentAsync(int? departmentId);
        Task<BoxDefinition?> GetByIdAsync(int boxId);
        Task<List<BoxSearchResult>> SearchAsync(string query, int limit);
        Task<int> CreateAsync(string name, string description, string cronExpression, string timeZoneId, bool allowParallel, string? notificationEmail, int? departmentId = null);
        Task<bool> UpdateAsync(int boxId, string name, string description, string cronExpression, string timeZoneId, bool allowParallel, bool enabled, string? notificationEmail, int? departmentId = null);
        Task<bool> DeleteAsync(int boxId);
        Task UpdateLastRunAsync(int boxId, DateTime lastRunUtc);

        // BoxRun operations
        Task<BoxRun?> GetBoxRunAsync(int boxRunId);
        Task<BoxRunSummary?> GetBoxRunSummaryAsync(int boxRunId);
        Task<List<BoxRunSummary>> GetRecentBoxRunsAsync(int limit = 100, int? boxId = null);
        Task<List<BoxRun>> GetRunningBoxRunsAsync();
        Task<int> CreateBoxRunAsync(int boxId, DateTime? scheduledForUtc, string triggerSource, int? requestedByUserId);
        Task UpdateBoxRunCancellationAsync(int boxRunId, bool isCancelled);
        Task UpdateBoxRunStatusAsync(int boxRunId, string status, DateTime? startedAtUtc = null, DateTime? endedAtUtc = null);
        Task<bool> HasBoxRunForScheduledTimeAsync(int boxId, DateTime scheduledForUtc);

        // BoxExecutionQueue operations
        Task<List<BoxQueueItem>> GetPendingQueueItemsAsync();
        Task MarkQueueItemAsync(int queueId, string status);
        Task<int> InsertQueueItemAsync(int boxId, int? userId, bool ignoreDependencies, bool ignoreSchedule, string? reason);
    }
}
