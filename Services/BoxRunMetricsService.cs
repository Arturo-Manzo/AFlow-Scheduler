using CHRONIQ.Api.Dtos;
using CHRONIQ.Data;

namespace CHRONIQ.Services;

public class BoxRunMetricsService : IBoxRunMetricsService
{
    private readonly IBoxRepository _boxRepository;
    private readonly ITaskRepository _taskRepository;
    private readonly IExecutionRepository _executionRepository;

    public BoxRunMetricsService(
        IBoxRepository boxRepository,
        ITaskRepository taskRepository,
        IExecutionRepository executionRepository)
    {
        ArgumentNullException.ThrowIfNull(boxRepository);
        ArgumentNullException.ThrowIfNull(taskRepository);
        ArgumentNullException.ThrowIfNull(executionRepository);

        _boxRepository = boxRepository;
        _taskRepository = taskRepository;
        _executionRepository = executionRepository;
    }

    public async Task<BoxRunMetricsDto?> GetMetricsAsync(int boxRunId)
    {
        var run = await _boxRepository.GetBoxRunAsync(boxRunId);
        if (run == null)
        {
            return null;
        }

        var tasks = await _taskRepository.GetTasksForBoxAsync(run.BoxId);
        var executions = await _executionRepository.GetExecutionsForBoxRunAsync(boxRunId);
        var latestByTaskId = executions
            .GroupBy(record => record.TaskId)
            .ToDictionary(group => group.Key, group => group.OrderByDescending(record => record.ExecutionId).First());

        var taskMetrics = tasks
            .OrderBy(task => task.Id)
            .Select(task =>
            {
                latestByTaskId.TryGetValue(task.Id, out var execution);
                var normalizedStatus = execution == null
                    ? "Pending"
                    : execution.Status switch
                    {
                        "NotExecuted" => "Skipped",
                        "Aborted" => "Skipped",
                        _ => execution.Status
                    };

                return new TaskMetricDto
                {
                    TaskId = task.Id,
                    Name = task.Name,
                    Status = normalizedStatus,
                    Duration = execution?.Duration,
                    DurationSeconds = execution?.Duration?.TotalSeconds is double seconds ? (int?)Math.Round(seconds) : null
                };
            })
            .ToList();

        var successCount = taskMetrics.Count(metric => metric.Status == "Success");
        var failedCount = taskMetrics.Count(metric => metric.Status is "Failed" or "Skipped");
        var pendingCount = taskMetrics.Count(metric => metric.Status is "Pending" or "Running");
        var totalTasks = taskMetrics.Count;
        var totalDuration = run.Duration;

        return new BoxRunMetricsDto
        {
            BoxRunId = boxRunId,
            TotalTasks = totalTasks,
            SuccessCount = successCount,
            FailedCount = failedCount,
            PendingCount = pendingCount,
            TotalDuration = totalDuration,
            TotalDurationSeconds = totalDuration.HasValue ? (int?)Math.Round(totalDuration.Value.TotalSeconds) : null,
            SuccessRate = totalTasks == 0 ? 0 : Math.Round((double)successCount / totalTasks * 100d, 2),
            Tasks = taskMetrics
        };
    }
}