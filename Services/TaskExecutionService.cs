using System.Collections.Concurrent;
using System.Diagnostics;
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
    private readonly IBoxRepository _boxRepository;
    private readonly ITaskRepository _taskRepository;
    private readonly ITaskFailureNotificationService _notificationService;
    private readonly ILogger<TaskExecutionService> _logger;
    private readonly int _taskTimeoutSeconds;

    // Guards against concurrent execution of the same task on this instance.
    // This is in-memory only; database constraints provide cross-instance protection.
    private readonly ConcurrentDictionary<int, byte> _runningTaskIds;

    public TaskExecutionService(
        IExecutionRepository executionRepository,
        IExecutionLogger executionLogger,
        ExecutorFactory executorFactory,
        IBoxRepository boxRepository,
        ITaskRepository taskRepository,
        ITaskFailureNotificationService notificationService,
        IConfiguration configuration,
        ILogger<TaskExecutionService> logger)
    {
        ArgumentNullException.ThrowIfNull(executionRepository);
        ArgumentNullException.ThrowIfNull(executionLogger);
        ArgumentNullException.ThrowIfNull(executorFactory);
        ArgumentNullException.ThrowIfNull(boxRepository);
        ArgumentNullException.ThrowIfNull(taskRepository);
        ArgumentNullException.ThrowIfNull(notificationService);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(logger);

        _executionRepository = executionRepository;
        _executionLogger = executionLogger;
        _executorFactory = executorFactory;
        _boxRepository = boxRepository;
        _taskRepository = taskRepository;
        _notificationService = notificationService;
        _logger = logger;
        _taskTimeoutSeconds = Math.Max(10, configuration.GetValue<int>("WorkerPool:TaskTimeoutSeconds", 300));
        _runningTaskIds = new ConcurrentDictionary<int, byte>();
    }

    /// <summary>
    /// Executes a task with concurrency guards, lifecycle persistence, logging, and failure notification.
    /// </summary>
    /// <param name="task">Task definition to execute.</param>
    /// <param name="boxRunId">Associated box run identifier, or null for force-start executions.</param>
    /// <param name="triggerSource">Trigger source value; normalized before persistence.</param>
    /// <param name="scheduledForUtc">Original scheduled UTC time, when applicable.</param>
    /// <param name="requestedByUserId">Requesting user identifier for manual/force-start flows, if available.</param>
    /// <param name="reason">Optional free-text reason for execution.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// True when task execution completes successfully (exit code 0);
    /// otherwise false when duplicate prevention, timeout, cancellation, or execution failure occurs.
    /// </returns>
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
                    
                    // Send notification if box has email configured
                    await SendTaskFailureNotificationAsync(
                        task, boxRunId, executionId, normalizedTriggerSource, scheduledForUtc,
                        failureReason: $"Exit code {result.ExitCode}: {(string.IsNullOrWhiteSpace(result.Error) ? result.Output : result.Error)}",
                        ct);
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

                // Send notification if box has email configured
                await SendTaskFailureNotificationAsync(
                    task, boxRunId, executionId, normalizedTriggerSource, scheduledForUtc,
                    failureReason: $"Timeout after {_taskTimeoutSeconds}s",
                    ct);

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

                // Send notification if box has email configured
                await SendTaskFailureNotificationAsync(
                    task, boxRunId, executionId, normalizedTriggerSource, scheduledForUtc,
                    failureReason: $"Exception: {ex.Message}",
                    ct);

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

    internal int ActiveCount => _runningTaskIds.Count;

    /// <summary>
    /// Helper method to send task failure notifications. Loads box details and sends email if configured.
    /// This method is best-effort; failures are logged but not propagated.
    /// </summary>
    private async Task SendTaskFailureNotificationAsync(
        TaskDefinition task,
        int? boxRunId,
        int executionId,
        string normalizedTriggerSource,
        DateTime? scheduledForUtc,
        string failureReason,
        CancellationToken ct)
    {
        var notificationStopwatch = Stopwatch.StartNew();
        try
        {
            _logger.LogInformation(
                "Preparing task failure notification. TaskId={TaskId}, TaskName={TaskName}, BoxId={BoxId}, BoxRunId={BoxRunId}, ExecutionId={ExecutionId}, TriggerSource={TriggerSource}",
                task.Id,
                task.Name,
                task.BoxId,
                boxRunId,
                executionId,
                normalizedTriggerSource);

            var box = await _boxRepository.GetByIdAsync(task.BoxId);
            if (box == null)
            {
                _logger.LogWarning(
                    "Notification skipped because box was not found. TaskId={TaskId}, BoxId={BoxId}, ExecutionId={ExecutionId}",
                    task.Id,
                    task.BoxId,
                    executionId);
                return;
            }

            if (string.IsNullOrWhiteSpace(box.NotificationEmail))
            {
                _logger.LogInformation(
                    "Notification skipped because box has no failure alert email. TaskId={TaskId}, BoxId={BoxId}, ExecutionId={ExecutionId}",
                    task.Id,
                    task.BoxId,
                    executionId);
                return; // No email configured for this box
            }

            var sent = await _notificationService.SendTaskFailureNotificationAsync(
                boxId: task.BoxId,
                taskId: task.Id,
                boxRunId: boxRunId,
                taskName: task.Name,
                boxName: box.Name,
                notificationEmail: box.NotificationEmail,
                failureReason: failureReason,
                executionId: executionId,
                triggerSource: normalizedTriggerSource,
                scheduledForUtc: scheduledForUtc,
                requestedByUsername: null, // Could be enriched from requestedByUserId if needed
                cancellationToken: ct);

            notificationStopwatch.Stop();
            if (sent)
            {
                _logger.LogInformation(
                    "Task failure notification sent successfully. TaskId={TaskId}, BoxId={BoxId}, ExecutionId={ExecutionId}, Email={Email}, ElapsedMs={ElapsedMs}",
                    task.Id,
                    task.BoxId,
                    executionId,
                    MaskEmail(box.NotificationEmail),
                    notificationStopwatch.ElapsedMilliseconds);
                return;
            }

            _logger.LogWarning(
                "Task failure notification attempt completed without send. TaskId={TaskId}, BoxId={BoxId}, ExecutionId={ExecutionId}, Email={Email}, ElapsedMs={ElapsedMs}",
                task.Id,
                task.BoxId,
                executionId,
                MaskEmail(box.NotificationEmail),
                notificationStopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            notificationStopwatch.Stop();
            _logger.LogError(
                ex,
                "Task failure notification pipeline failed. TaskId={TaskId}, BoxId={BoxId}, ExecutionId={ExecutionId}, TriggerSource={TriggerSource}, ElapsedMs={ElapsedMs}",
                task.Id,
                task.BoxId,
                executionId,
                normalizedTriggerSource,
                notificationStopwatch.ElapsedMilliseconds);
        }
    }

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

        // Send notification if box has email configured
        try
        {
            var task = await _taskRepository.GetByIdAsync(taskId);
            if (task == null)
            {
                _logger.LogWarning(
                    "Could not load task for failed-execution notification. TaskId={TaskId}, BoxRunId={BoxRunId}, ExecutionId={ExecutionId}",
                    taskId,
                    boxRunId,
                    executionId);
                return;
            }

            await SendTaskFailureNotificationAsync(
                task,
                boxRunId,
                executionId,
                normalizedTriggerSource,
                scheduledForUtc,
                failureReason,
                CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed-execution notification path failed. TaskId={TaskId}, BoxRunId={BoxRunId}, ExecutionId={ExecutionId}",
                taskId,
                boxRunId,
                executionId);
        }
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

    private static string MaskEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return "<empty>";

        var atIndex = email.IndexOf('@');
        if (atIndex <= 1 || atIndex == email.Length - 1)
            return "***";

        var local = email[..atIndex];
        var domain = email[(atIndex + 1)..];
        var visibleLocal = local.Length <= 2 ? local[0].ToString() : local[..2];
        return $"{visibleLocal}***@{domain}";
    }
}
