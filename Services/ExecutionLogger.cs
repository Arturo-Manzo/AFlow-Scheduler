using CHRONIQ.Data;
using CHRONIQ.Domain;

namespace CHRONIQ.Services;

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
        var normalizedMessage = Normalize(message);
        var normalizedDetails = Normalize(details);

        if (string.Equals(normalizedMessage, normalizedDetails, StringComparison.Ordinal))
        {
            normalizedDetails = string.Empty;
        }

        return PersistSafeAsync(new TaskExecutionLog
        {
            Id = Guid.NewGuid(),
            BoxRunId = boxRunId,
            TaskId = taskId,
            TaskExecutionId = taskExecutionId,
            Timestamp = DateTime.UtcNow,
            Level = "Error",
            Message = normalizedMessage,
            Details = normalizedDetails
        });
    }

    private static string Normalize(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
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
