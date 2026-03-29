using AScheduler.Domain;

namespace AScheduler.Data
{
    public interface IBoxRepository
    {
        Task<List<BoxDefinition>> GetActiveBoxesAsync();
        Task<BoxDefinition?> GetByIdAsync(int boxId);
        Task<int> CreateAsync(string name, string description, string cronExpression, bool allowParallel);
        Task<bool> UpdateAsync(int boxId, string name, string description, string cronExpression, bool allowParallel, bool enabled);
        Task<bool> DeleteAsync(int boxId);
        Task UpdateLastRunAsync(int boxId, DateTime lastRunUtc);

        // BoxRun operations
        Task<int> CreateBoxRunAsync(int boxId, DateTime? scheduledForUtc, string triggerSource, int? requestedByUserId);
        Task UpdateBoxRunStatusAsync(int boxRunId, string status, DateTime? startedAtUtc = null, DateTime? endedAtUtc = null);
        Task<bool> HasBoxRunForScheduledTimeAsync(int boxId, DateTime scheduledForUtc);

        // BoxExecutionQueue operations
        Task<List<BoxQueueItem>> GetPendingQueueItemsAsync();
        Task MarkQueueItemAsync(int queueId, string status);
        Task<int> InsertQueueItemAsync(int boxId, int? userId, bool ignoreDependencies, bool ignoreSchedule, string? reason);
    }
}
