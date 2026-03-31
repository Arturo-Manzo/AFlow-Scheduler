using System.Collections.Concurrent;
using AScheduler.Data;
using AScheduler.Domain;
using AScheduler.Execution;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace AScheduler.Services;

/// <summary>
/// Production implementation of the centralized task execution service.
/// This is the ONLY place where CreateExecutionAsync is called.
/// All execution paths funnel through ExecuteTaskAsync.
/// </summary>
public class TaskExecutionService : ITaskExecutionService
{
    private readonly IExecutionRepository _executionRepository;
    private readonly IExecutionLogger _executionLogger;
    private readonly ExecutorFactory _executorFactory;
    private readonly ILogger<TaskExecutionService> _logger;
    private readonly int _taskTimeoutSeconds;

    // Guards against concurrent execution of the same task on this instance.
    // This is in-memory only; database constraints provide cross-instance protection.
    private readonly ConcurrentDictionary<int, byte> _runningTaskIds;

    public TaskExecutionService(
        IExecutionRepository executionRepository,
        IExecutionLogger executionLogger,
        ExecutorFactory executorFactory,
        IConfiguration configuration,
        ILogger<TaskExecutionService> logger)
    {
        ArgumentNullException.ThrowIfNull(executionRepository);
        ArgumentNullException.ThrowIfNull(executionLogger);
        ArgumentNullException.ThrowIfNull(executorFactory);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(logger);

        _executionRepository = executionRepository;
        _executionLogger = executionLogger;
        _executorFactory = executorFactory;
        _logger = logger;
        _taskTimeoutSeconds = Math.Max(10, configuration.GetValue<int>("WorkerPool:TaskTimeoutSeconds", 300));
        _runningTaskIds = new ConcurrentDictionary<int, byte>();
    }

    public async Task<bool> ExecuteTaskAsync(
        TaskDefinition task,
        int? boxRunId,
        string triggerSource,
        DateTime? scheduledForUtc,
        int? requestedByUserId,
        string? reason,
        CancellationToken ct)
    {
        var normalizedTriggerSource = TriggerSources.Normalize(triggerSource);

        // In-process guard: rejects if this task is already executing on any worker in this instance.
        if (!_runningTaskIds.TryAdd(task.Id, 0))
        {
            _logger.LogWarning(
                "Task {TaskId} ({TaskName}) is already running; skipping to avoid duplicate execution.",
                task.Id, task.Name);
            return false;
        }

        try
        {
            if (boxRunId.HasValue)
            {
                var latestExecution = await _executionRepository.GetLastExecutionForTaskInBoxRunAsync(task.Id, boxRunId.Value);
                if (latestExecution?.Status == "Success")
                {
                    _logger.LogInformation(
                        "Task {TaskId} ({TaskName}) already succeeded for BoxRun {BoxRunId}; skipping duplicate execution.",
                        task.Id,
                        task.Name,
                        boxRunId.Value);
                    return true;
                }

                if (latestExecution?.Status == "Running")
                {
                    _logger.LogWarning(
                        "Task {TaskId} ({TaskName}) is already marked Running for BoxRun {BoxRunId}; skipping duplicate execution attempt.",
                        task.Id,
                        task.Name,
                        boxRunId.Value);
                    return false;
                }
            }

            var startTime = DateTime.UtcNow;
            int executionId;
            try
            {
                // Attempt to create execution record. Database constraints will reject cross-instance duplicates.
                executionId = await _executionRepository.CreateExecutionAsync(
                    task.Id, boxRunId, startTime, normalizedTriggerSource, scheduledForUtc, requestedByUserId, reason);
            }
            catch (SqlException ex) when (ex.Number == 2601 || ex.Number == 2627)
            {
                // DB-level unique constraint violation — concurrent execution started across instances.
                _logger.LogWarning(
                    "Task {TaskId} ({TaskName}): DB constraint blocked a duplicate execution (cross-instance race).",
                    task.Id, task.Name);
                return false;
            }

            await _executionLogger.LogInfo(boxRunId, executionId, task.Id, "Task started");

            // Execute the task
            var executor = _executorFactory.GetExecutor(task.TaskType);
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(_taskTimeoutSeconds));

            try
            {
                var result = await executor.ExecuteAsync(task);
                var endTime = DateTime.UtcNow;
                var status = result.ExitCode == 0 ? "Success" : "Failed";

                await _executionRepository.CompleteExecutionAsync(
                    executionId, endTime, status,
                    result.Output, result.Error, result.ExitCode,
                    result.Output, result.Error);

                if (result.ExitCode == 0)
                {
                    await _executionLogger.LogInfo(boxRunId, executionId, task.Id, "Task completed successfully");
                }
                else
                {
                    await _executionLogger.LogError(
                        boxRunId,
                        executionId,
                        task.Id,
                        "Task failed",
                        string.IsNullOrWhiteSpace(result.Error) ? result.Output : result.Error);
                }

                _logger.LogInformation(
                    "Task {TaskId} ({TaskName}) finished with status {Status} (exit code {ExitCode}).",
                    task.Id, task.Name, status, result.ExitCode);
                return result.ExitCode == 0;
            }
            catch (OperationCanceledException)
            {
                await _executionRepository.CompleteExecutionAsync(
                    executionId, DateTime.UtcNow, "Failed",
                    "", $"Timeout after {_taskTimeoutSeconds}s", -1,
                    "", $"Timeout after {_taskTimeoutSeconds}s");

                await _executionLogger.LogError(
                    boxRunId,
                    executionId,
                    task.Id,
                    "Task failed",
                    $"Timeout after {_taskTimeoutSeconds}s");

                _logger.LogWarning(
                    "Task {TaskId} ({TaskName}) timed out after {Seconds}s.",
                    task.Id, task.Name, _taskTimeoutSeconds);
                return false;
            }
            catch (Exception ex)
            {
                await _executionRepository.CompleteExecutionAsync(
                    executionId, DateTime.UtcNow, "Failed",
                    "", ex.Message, -1,
                    "", ex.ToString());

                await _executionLogger.LogError(boxRunId, executionId, task.Id, "Task failed", ex.ToString());

                _logger.LogError(ex, "Task {TaskId} ({TaskName}) failed with exception.", task.Id, task.Name);
                return false;
            }
        }
        finally
        {
            _runningTaskIds.TryRemove(task.Id, out _);
        }
    }

    /// <remarks>
    /// Internal helper for querying in-process state (used by IWorkerStateService).
    /// </remarks>
    internal bool IsTaskRunning(int taskId) => _runningTaskIds.ContainsKey(taskId);

    /// <remarks>
    /// Internal helper for creating pre-failed executions (e.g., blocked tasks due to dependency failures).
    /// This is an exception to the normal flow - it creates a Failed execution without running anything.
    /// </remarks>
    internal async Task CreateFailedExecutionAsync(
        int taskId,
        int? boxRunId,
        DateTime failedAtUtc,
        string triggerSource,
        DateTime? scheduledForUtc,
        int? requestedByUserId,
        string failureReason)
    {
        var normalizedTriggerSource = TriggerSources.Normalize(triggerSource);

        // Create execution record in Running state first
        var executionId = await _executionRepository.CreateExecutionAsync(
            taskId, boxRunId, failedAtUtc, normalizedTriggerSource, scheduledForUtc, requestedByUserId, null);

        await _executionLogger.LogInfo(boxRunId, executionId, taskId, "Task started");
        
        // Immediately mark as Failed without running anything
        await _executionRepository.CompleteExecutionAsync(
            executionId, failedAtUtc, "Failed", "", failureReason, -2, "", failureReason);

        await _executionLogger.LogError(boxRunId, executionId, taskId, "Task failed", failureReason);
    }

    /// <remarks>
    /// Internal helper for creating terminal NotExecuted executions when a task is blocked by failed dependencies.
    /// </remarks>
    internal async Task CreateNotExecutedExecutionAsync(
        int taskId,
        int? boxRunId,
        DateTime markedAtUtc,
        string triggerSource,
        DateTime? scheduledForUtc,
        int? requestedByUserId,
        string reason)
    {
        var normalizedTriggerSource = TriggerSources.Normalize(triggerSource);

        var executionId = await _executionRepository.CreateExecutionAsync(
            taskId, boxRunId, markedAtUtc, normalizedTriggerSource, scheduledForUtc, requestedByUserId, null);

        await _executionLogger.LogInfo(boxRunId, executionId, taskId, "Task started");

        await _executionRepository.CompleteExecutionAsync(
            executionId, markedAtUtc, "NotExecuted", "", reason, null, "", reason);

        await _executionLogger.LogError(boxRunId, executionId, taskId, "Task failed", reason);
    }
}
