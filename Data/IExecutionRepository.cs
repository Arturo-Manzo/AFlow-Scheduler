using AScheduler.Domain;

namespace AScheduler.Data
{
    public interface IExecutionRepository
    {
        Task<List<ExecutionRepository.ExecutionRecord>> GetExecutionsForBoxRunAsync(int boxRunId);
        Task<List<ExecutionRepository.ExecutionRecord>> GetExecutionsForTaskAsync(int taskId, DateTime? fromUtc = null, DateTime? toUtc = null);
        Task<ExecutionRepository.ExecutionRecord?> GetLastExecutionForTaskAsync(int taskId);
        Task<List<ExecutionRepository.ExecutionRecord>> GetRunningExecutionsAsync(DateTime staleBeforeUtc);
        Task<int> CreateExecutionAsync(int taskId, int? boxRunId, DateTime startedAtUtc,
            string triggerSource, DateTime? scheduledForUtc, int? requestedByUserId, string? reason);
        Task CompleteExecutionAsync(int executionId, DateTime endedAtUtc, string status,
            string output, string error, int? exitCode, string stdOut, string stdErr);
        Task<int> AbortRunningExecutionsAsync(DateTime endedAtUtc, string reason);
        Task<ExecutionRepository.ExecutionRecord?> GetLastExecutionForTaskInBoxRunAsync(int taskId, int boxRunId);
        Task<List<ExecutionRepository.ExecutionRecord>> GetLatestExecutionsAsync(int limit = 20);
        /// <summary>
        /// Returns the latest execution status for each task in the given BoxRun.
        /// Used to seed in-memory state when resuming an interrupted BoxRun.
        /// </summary>
        Task<Dictionary<int, string>> GetTaskStatusMapForBoxRunAsync(int boxRunId);
        Task<int> FailRunningExecutionsForBoxRunAsync(int boxRunId, DateTime endedAtUtc, string reason);
        Task AddLogAsync(TaskExecutionLog log);
        Task<List<TaskExecutionLog>> GetLogsByTaskExecutionIdAsync(int taskExecutionId);
        Task<List<TaskExecutionLog>> GetLogsByBoxRunIdAsync(int boxRunId);
    }
}
