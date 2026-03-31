using AScheduler.Data;
using AScheduler.Domain;

namespace AScheduler.Services;

public class ExecutionLogger : IExecutionLogger
{
    private readonly IExecutionRepository _executionRepository;
    private readonly ILogger<ExecutionLogger> _logger;

    public ExecutionLogger(IExecutionRepository executionRepository, ILogger<ExecutionLogger> logger)
    {
        ArgumentNullException.ThrowIfNull(executionRepository);
        ArgumentNullException.ThrowIfNull(logger);

        _executionRepository = executionRepository;
        _logger = logger;
    }

    public Task LogInfo(int? boxRunId, int taskExecutionId, int taskId, string message)
    {
        return PersistSafeAsync(new TaskExecutionLog
        {
            Id = Guid.NewGuid(),
            BoxRunId = boxRunId,
            TaskId = taskId,
            TaskExecutionId = taskExecutionId,
            Timestamp = DateTime.UtcNow,
            Level = "Info",
            Message = message
        });
    }

    public Task LogError(int? boxRunId, int taskExecutionId, int taskId, string message, string details)
    {
        return PersistSafeAsync(new TaskExecutionLog
        {
            Id = Guid.NewGuid(),
            BoxRunId = boxRunId,
            TaskId = taskId,
            TaskExecutionId = taskExecutionId,
            Timestamp = DateTime.UtcNow,
            Level = "Error",
            Message = message,
            Details = details
        });
    }

    private async Task PersistSafeAsync(TaskExecutionLog log)
    {
        try
        {
            await _executionRepository.AddLogAsync(log);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Failed to persist execution log for Execution {ExecutionId}, Task {TaskId}.",
                log.TaskExecutionId,
                log.TaskId);
        }
    }
}