namespace CHRONIQ.Services;

public interface IExecutionLogger
{
    Task LogInfo(int? boxRunId, int taskExecutionId, int taskId, string message);
    Task LogError(int? boxRunId, int taskExecutionId, int taskId, string message, string details);
}