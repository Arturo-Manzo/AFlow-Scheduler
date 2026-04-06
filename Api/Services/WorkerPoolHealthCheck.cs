using CHRONIQ.Data;
using CHRONIQ.Queue;
using CHRONIQ.Services;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace CHRONIQ.Api.Services;

/// <summary>
/// Validates worker pool operational readiness for scheduler traffic.
/// </summary>
public sealed class WorkerPoolHealthCheck(
    IWorkerStateService workerStateService,
    ITaskQueue taskQueue,
    IExecutionRepository executionRepository,
    IStaleThresholdProvider staleThresholdProvider,
    IConfiguration configuration) : IHealthCheck
{
    private readonly IWorkerStateService _workerStateService = workerStateService
        ?? throw new ArgumentNullException(nameof(workerStateService));
    private readonly ITaskQueue _taskQueue = taskQueue
        ?? throw new ArgumentNullException(nameof(taskQueue));
    private readonly IExecutionRepository _executionRepository = executionRepository
        ?? throw new ArgumentNullException(nameof(executionRepository));
    private readonly IStaleThresholdProvider _staleThresholdProvider = staleThresholdProvider
        ?? throw new ArgumentNullException(nameof(staleThresholdProvider));
    private readonly IConfiguration _configuration = configuration
        ?? throw new ArgumentNullException(nameof(configuration));

    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var totalWorkers = _workerStateService.TotalWorkerCount;
        var queueDepth = _taskQueue.QueueDepth;

        if (totalWorkers <= 0)
        {
            return HealthCheckResult.Unhealthy("Worker pool has no configured workers.");
        }

        var staleThresholdMinutes = Math.Max(1, _configuration.GetValue<int>("WorkerPool:StaleExecutionThresholdMinutes", 15));
        var queueDepthDegradedThreshold = Math.Max(1, _configuration.GetValue<int>("WorkerPool:QueueDepthDegradedThreshold", 50));
        var staleBeforeUtc = DateTime.UtcNow.AddMinutes(-staleThresholdMinutes);
        var runningExecutions = await _executionRepository.GetRunningExecutionsAsync(staleBeforeUtc);

        // Re-compute IsStale per-task using dynamic thresholds
        foreach (var record in runningExecutions)
        {
            record.IsStale = await _staleThresholdProvider.IsStaleAsync(record.TaskId, record.StartedAt);
        }

        var staleCount = runningExecutions.Count(record => record.IsStale);

        var data = new Dictionary<string, object>
        {
            ["totalWorkers"] = totalWorkers,
            ["activeWorkers"] = _workerStateService.ActiveWorkerCount,
            ["runningBoxRuns"] = _workerStateService.RunningBoxRunCount,
            ["queueDepth"] = queueDepth,
            ["runningExecutions"] = runningExecutions.Count,
            ["staleExecutions"] = staleCount,
            ["staleThresholdMinutes"] = staleThresholdMinutes
        };

        if (staleCount > 0 || queueDepth >= queueDepthDegradedThreshold)
        {
            return HealthCheckResult.Degraded("Worker pool is under pressure (stale executions or high queue depth).", data: data);
        }

        return HealthCheckResult.Healthy("Worker pool is ready.", data);
    }
}