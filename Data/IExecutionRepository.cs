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
    }
}
