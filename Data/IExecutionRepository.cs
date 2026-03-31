namespace AScheduler.Data
{
    public interface IExecutionRepository
    {
        Task<List<ExecutionRepository.ExecutionRecord>> GetExecutionsForBoxRunAsync(int boxRunId);
        Task<List<ExecutionRepository.ExecutionRecord>> GetExecutionsForTaskAsync(int taskId);
        Task SaveExecutionAsync(int taskId, int boxRunId, DateTime startedAt, DateTime endedAt,
            string status, string output, string error, int exitCode,
            string stdOut, string stdErr, string triggerSource, DateTime? scheduledForUtc);
        Task<ExecutionRepository.ExecutionRecord?> GetLastExecutionForTaskInBoxRunAsync(int taskId, int boxRunId);
        Task<List<ExecutionRepository.ExecutionRecord>> GetLatestExecutionsAsync(int limit = 20);

        /// <summary>
        /// Saves the result of an isolated TaskForceStart execution.
        /// No BoxRun is associated — BoxRunId is stored as NULL in the database.
        /// These records are intentionally excluded from BoxRun-based history queries.
        /// </summary>
        Task SaveDirectExecutionAsync(int taskId, DateTime startedAt, DateTime endedAt,
            string status, string output, string error, int exitCode, string stdOut, string stdErr,
            int? requestedByUserId, string reason);
    }
}
