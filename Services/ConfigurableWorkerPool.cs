using System.Collections.Concurrent;
using CHRONIQ.Data;
using CHRONIQ.Domain;
using CHRONIQ.Queue;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CHRONIQ.Services;

public class ConfigurableWorkerPool : BackgroundService, IWorkerStateService
{
    private readonly IBoxRepository _boxRepository;
    private readonly ITaskRepository _taskRepository;
    private readonly IExecutionRepository _executionRepository;
    private readonly ITaskQueue _taskQueue;
    private readonly ITaskExecutionService _taskExecutionService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ConfigurableWorkerPool> _logger;

    // Guards against the same BoxRun being picked up by two workers simultaneously (single-process safety only).
    private readonly ConcurrentDictionary<int, byte> _runningBoxRunIds = new();

    // Guards against concurrent execution of two different BoxRuns for the same Box.
    // LIMITATION: This is in-memory only. If the application runs as multiple instances
    // (horizontal scaling), the same BoxId can execute concurrently across instances.
    // Distributed locking (e.g. DB-level status check or Redis) is required for that scenario.
    private readonly ConcurrentDictionary<int, byte> _runningBoxIds = new();

    private int _workerCount;
    private readonly bool _autoRecoverStaleExecutions;
    private readonly SemaphoreSlim _taskExecutionSlots;
    private List<Task> _workerTasks = new();
    private CancellationTokenSource? _stoppingCts;
    private DateTime? _lastRecoveryCompletedAtUtc;
    private int _lastRecoveredExecutionCount;
    private int _lastRecoveredBoxRunCount;
    private volatile bool _startupRecoveryCompleted;

    public ConfigurableWorkerPool(
        IBoxRepository boxRepository,
        ITaskRepository taskRepository,
        IExecutionRepository executionRepository,
        ITaskQueue taskQueue,
        ITaskExecutionService taskExecutionService,
        IConfiguration configuration,
        ILogger<ConfigurableWorkerPool> logger)
    {
        ArgumentNullException.ThrowIfNull(boxRepository);
        ArgumentNullException.ThrowIfNull(taskRepository);
        ArgumentNullException.ThrowIfNull(executionRepository);
        ArgumentNullException.ThrowIfNull(taskQueue);
        ArgumentNullException.ThrowIfNull(taskExecutionService);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(logger);

        _boxRepository = boxRepository;
        _taskRepository = taskRepository;
        _executionRepository = executionRepository;
        _taskQueue = taskQueue;
        _taskExecutionService = taskExecutionService;
        _configuration = configuration;
        _logger = logger;

        _workerCount = Math.Max(1, Math.Min(_configuration.GetValue<int>("WorkerPool:WorkerCount", 4), 20));
        _autoRecoverStaleExecutions = _configuration.GetValue<bool>("WorkerPool:AutoRecoverStaleExecutions", true);
        _taskExecutionSlots = new SemaphoreSlim(_workerCount, _workerCount);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _stoppingCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);

        _logger.LogInformation("Worker pool starting with {WorkerCount} workers.", _workerCount);

        try
        {
            if (_autoRecoverStaleExecutions)
            {
                try
                {
                    await RecoverStaleExecutionsAsync();
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    MarkStartupRecoveryDeferred(ex);
                }
            }
            else
            {
                MarkStartupRecoverySkipped();
            }

            _workerTasks = Enumerable.Range(1, _workerCount)
                .Select(id => ProcessWorkerAsync(id, _stoppingCts.Token))
                .ToList();
            await Task.WhenAll(_workerTasks);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fatal error in worker pool.");
            throw;
        }
    }

    private async Task ProcessWorkerAsync(int workerId, CancellationToken ct)
    {
        _logger.LogInformation("Worker {WorkerId} started.", workerId);
        try
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    var item = await _taskQueue.DequeueAsync(ct);
                    switch (item)
                    {
                        case BoxRunItem boxRunItem:
                            await ExecuteBoxRunAsync(workerId, boxRunItem.Request, ct);
                            break;
                        case TaskForceStartItem forceStartItem:
                            await ExecuteTaskForceStartAsync(workerId, forceStartItem.Request, ct);
                            break;
                        default:
                            _logger.LogError("Worker {WorkerId}: unknown WorkerItem type {Type}.", workerId, item.GetType().Name);
                            break;
                    }
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in worker {WorkerId}.", workerId);
                    await Task.Delay(500, ct);
                }
            }
        }
        finally { _logger.LogInformation("Worker {WorkerId} stopped.", workerId); }
    }

    private async Task ExecuteBoxRunAsync(int workerId, BoxRunRequest request, CancellationToken ct)
    {
        var boxRunId = request.BoxRunId;
        var boxId = request.BoxId;

        if (!_runningBoxRunIds.TryAdd(boxRunId, 0))
        {
            _logger.LogWarning("BoxRun {BoxRunId} already executing, skipping.", boxRunId);
            return;
        }

        // Prevent concurrent execution of two BoxRuns belonging to the same Box.
        // If another BoxRun for the same BoxId is already running, requeue this request and bail.
        // The queued item will be retried once the worker picks it up again.
        if (!_runningBoxIds.TryAdd(boxId, 0))
        {
            _logger.LogWarning(
                "BoxId {BoxId} is already executing. BoxRun {BoxRunId} will be requeued and retried.",
                boxId, boxRunId);
            _runningBoxRunIds.TryRemove(boxRunId, out _);
            await _taskQueue.EnqueueAsync(request);
            return;
        }

        try
        {
            // On resume, the BoxRun is already Running — skip re-setting status.
            if (!request.IsResume)
            {
                await _boxRepository.UpdateBoxRunStatusAsync(boxRunId, "Running", DateTime.UtcNow, null);
            }
            else
            {
                await _boxRepository.UpdateBoxRunCancellationAsync(boxRunId, false);
            }
            _logger.LogInformation("Worker {WorkerId} {Action} BoxRun {BoxRunId} for Box {BoxId}.",
                workerId, request.IsResume ? "resuming" : "executing", boxRunId, boxId);

            var tasks = await _taskRepository.GetTasksForBoxAsync(boxId);
            if (tasks.Count == 0)
            {
                _logger.LogWarning("BoxRun {BoxRunId}: no tasks found for Box {BoxId}.", boxRunId, boxId);
                await FinalizeBoxRun(boxRunId, boxId, "Completed");
                return;
            }

            var anyTerminalNonSuccess = false;
            var anySuccess = false;
            var taskById = tasks.ToDictionary(t => t.Id);
            var pendingTaskIds = taskById.Keys.ToHashSet();
            var completedSuccess = new HashSet<int>();
            var completedNonSuccess = new HashSet<int>();

            var dependenciesByTaskId = new Dictionary<int, List<TaskDependency>>();
            var dependencyTaskIdsByTaskId = await _taskRepository.GetDependenciesBatchAsync(taskById.Keys);
            foreach (var taskId in taskById.Keys)
            {
                dependencyTaskIdsByTaskId.TryGetValue(taskId, out var dependencyTaskIds);
                dependenciesByTaskId[taskId] = (dependencyTaskIds ?? Enumerable.Empty<int>())
                    .Select(depId => new TaskDependency { TaskId = taskId, DependsOnTaskId = depId })
                    .ToList();
            }

            if (TryDetectDependencyCycle(dependenciesByTaskId, out var cyclePath))
            {
                var cycleError = $"Circular dependency detected: {cyclePath}";
                _logger.LogError("BoxRun {BoxRunId}: {CycleError}", boxRunId, cycleError);
                await MarkBlockedTasksAsNotExecutedAsync(tasks, request, cycleError);
                await FinalizeBoxRun(boxRunId, boxId, "Failed");
                return;
            }

            // --- Resume support: seed in-memory state from database ---
            if (request.IsResume)
            {
                var taskStatusMap = await _executionRepository.GetTaskStatusMapForBoxRunAsync(boxRunId);
                foreach (var (taskId, status) in taskStatusMap)
                {
                    if (!pendingTaskIds.Contains(taskId)) continue;

                    if (status == "Success")
                    {
                        completedSuccess.Add(taskId);
                        pendingTaskIds.Remove(taskId);
                        anySuccess = true;
                    }
                    // Failed/NotExecuted/Aborted stay in pending → will be retried by the DAG loop.
                }

                _logger.LogInformation(
                    "BoxRun {BoxRunId} resumed: {SuccessCount} succeeded (skipped), {PendingCount} pending for execution.",
                    boxRunId, completedSuccess.Count, pendingTaskIds.Count);

                if (pendingTaskIds.Count == 0)
                {
                    _logger.LogInformation("BoxRun {BoxRunId}: all tasks already succeeded, nothing to resume.", boxRunId);
                    await FinalizeBoxRun(boxRunId, boxId, "Completed");
                    return;
                }
            }

            var batchNumber = 1;
            var cancellationTookEffect = false;
            while (pendingTaskIds.Count > 0)
            {
                if (await ShouldStopForCancellationAsync(boxRunId, pendingTaskIds))
                {
                    cancellationTookEffect = true;
                    break;
                }

                List<int> readyTaskIds;
                if (request.ForceIgnoreDependencies)
                {
                    readyTaskIds = pendingTaskIds
                        .OrderBy(id => id)
                        .ToList();
                }
                else
                {
                    readyTaskIds = pendingTaskIds
                        .Where(taskId => dependenciesByTaskId[taskId].All(dep => completedSuccess.Contains(dep.DependsOnTaskId)))
                        .OrderBy(id => id)
                        .ToList();
                }

                if (await ShouldStopForCancellationAsync(boxRunId, pendingTaskIds))
                {
                    readyTaskIds.Clear();
                }

                if (readyTaskIds.Count == 0)
                {
                    if (await ShouldStopForCancellationAsync(boxRunId, pendingTaskIds))
                    {
                        cancellationTookEffect = true;
                        break;
                    }

                    // No task can move forward: this indicates a cycle or invalid dependency references.
                    _logger.LogWarning("BoxRun {BoxRunId}: unresolved dependency graph. Remaining tasks: {RemainingTaskIds}", boxRunId, string.Join(',', pendingTaskIds));
                    await MarkBlockedTasksAsNotExecutedAsync(
                        pendingTaskIds.Select(id => taskById[id]).ToList(),
                        request,
                        "Task was not executed because one or more dependencies were unresolved.");
                    anyTerminalNonSuccess = true;
                    break;
                }

                var results = await ExecuteReadyTaskBatchAsync(
                    readyTaskIds,
                    taskById,
                    boxRunId,
                    request,
                    batchNumber,
                    ct);
                batchNumber++;

                foreach (var result in results)
                {
                    pendingTaskIds.Remove(result.TaskId);

                    if (result.Success)
                    {
                        completedSuccess.Add(result.TaskId);
                        anySuccess = true;
                    }
                    else
                    {
                        completedNonSuccess.Add(result.TaskId);
                        anyTerminalNonSuccess = true;
                    }
                }

                if (!request.ForceIgnoreDependencies)
                {
                    var blockedTaskIds = pendingTaskIds
                        .Where(taskId => dependenciesByTaskId[taskId].Any(dep => completedNonSuccess.Contains(dep.DependsOnTaskId)))
                        .OrderBy(id => id)
                        .ToList();

                    if (blockedTaskIds.Count > 0)
                    {
                        await MarkBlockedTasksAsNotExecutedAsync(
                            blockedTaskIds.Select(id => taskById[id]).ToList(),
                            request,
                            "Task was not executed because one or more dependencies failed or were not executed.");

                        foreach (var blockedTaskId in blockedTaskIds)
                        {
                            pendingTaskIds.Remove(blockedTaskId);
                            completedNonSuccess.Add(blockedTaskId);
                            anyTerminalNonSuccess = true;
                        }
                    }
                }
            }

            var recoveredRunningExecutions = await _executionRepository.FailRunningExecutionsForBoxRunAsync(
                boxRunId,
                DateTime.UtcNow,
                "Task remained in Running state at BoxRun finalization and was force-failed for consistency.");
            if (recoveredRunningExecutions > 0)
            {
                anyTerminalNonSuccess = true;
                _logger.LogWarning(
                    "BoxRun {BoxRunId}: force-failed {Count} task execution(s) still marked Running during final consistency check.",
                    boxRunId,
                    recoveredRunningExecutions);
            }

            // Determine final status based on execution outcome:
            // - "Completed": all tasks succeeded
            // - "Partial": mixed success and non-success (Failed/NotExecuted)
            // - "Failed": no task succeeded
            var hasPendingTasks = HasPendingTasks(pendingTaskIds);
            var finalStatus = cancellationTookEffect && hasPendingTasks
                ? "Cancelled"
                : (!anyTerminalNonSuccess ? "Completed" : (anySuccess ? "Partial" : "Failed"));

            await FinalizeBoxRun(boxRunId, boxId, finalStatus);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "BoxRun {BoxRunId} fatal error.", boxRunId);
            await FinalizeBoxRun(boxRunId, boxId, "Failed");
        }
        finally
        {
            _runningBoxIds.TryRemove(boxId, out _);
            _runningBoxRunIds.TryRemove(boxRunId, out _);
        }
    }

    private async Task FinalizeBoxRun(int boxRunId, int boxId, string status)
    {
        var endTime = DateTime.UtcNow;
        if (!string.Equals(status, "Cancelled", StringComparison.OrdinalIgnoreCase))
        {
            await _boxRepository.UpdateBoxRunCancellationAsync(boxRunId, false);
        }
        await _boxRepository.UpdateBoxRunStatusAsync(boxRunId, status, null, endTime);
        await _boxRepository.UpdateLastRunAsync(boxId, endTime);
        _logger.LogInformation("BoxRun {BoxRunId} finalized with status {Status}.", boxRunId, status);
    }

    private bool HasPendingTasks(IReadOnlyCollection<int> pendingTaskIds)
    {
        return pendingTaskIds.Count > 0;
    }

    private async Task<bool> ShouldStopForCancellationAsync(int boxRunId, IReadOnlyCollection<int> pendingTaskIds)
    {
        if (!HasPendingTasks(pendingTaskIds))
        {
            return false;
        }

        var boxRun = await _boxRepository.GetBoxRunAsync(boxRunId);
        return boxRun?.IsCancelled == true;
    }

    // -------------------------------------------------------------------------
    // ISOLATED TASK FORCE-START PATH
    // Completely separate from BoxRun execution. No BoxRun is created or updated.
    // -------------------------------------------------------------------------

    private async Task ExecuteTaskForceStartAsync(int workerId, TaskForceStartRequest request, CancellationToken ct)
    {
        var task = await _taskRepository.GetByIdAsync(request.TaskId);
        if (task == null)
        {
            _logger.LogError("ForceStart: Task {TaskId} not found at execution time.", request.TaskId);
            return;
        }

        _logger.LogInformation("Worker {WorkerId} executing force-start for Task {TaskId} ({TaskName}).",
            workerId, request.TaskId, task.Name);

        // Delegate to centralized execution service (ONLY entry point)
        await _taskExecutionService.ExecuteTaskAsync(
            task, null, TriggerSources.ForceStart, null, request.RequestedByUserId, request.Reason, ct);
    }

    private async Task<(int TaskId, bool Success)> ExecuteTaskAsync(TaskDefinition task, int boxRunId, BoxRunRequest request, CancellationToken ct)
    {
        await _taskExecutionSlots.WaitAsync(ct);
        try
        {
            // Delegate to centralized execution service (ONLY entry point)
            var success = await _taskExecutionService.ExecuteTaskAsync(
                task, boxRunId, request.TriggerSource, request.ScheduledForUtc, request.RequestedByUserId, null, ct);
            return (task.Id, success);
        }
        finally
        {
            _taskExecutionSlots.Release();
        }
    }

    private async Task<List<(int TaskId, bool Success)>> ExecuteReadyTaskBatchAsync(
        List<int> readyTaskIds,
        IReadOnlyDictionary<int, TaskDefinition> taskById,
        int boxRunId,
        BoxRunRequest request,
        int batchNumber,
        CancellationToken ct)
    {
        _logger.LogInformation(
            "BoxRun {BoxRunId}: starting batch {BatchNumber} with {TaskCount} parallel task(s): {TaskIds}",
            boxRunId,
            batchNumber,
            readyTaskIds.Count,
            string.Join(',', readyTaskIds));

        var executionTasks = readyTaskIds
            .Select(taskId => ExecuteTaskAsync(taskById[taskId], boxRunId, request, ct))
            .ToList();

        var results = await Task.WhenAll(executionTasks);

        _logger.LogInformation(
            "BoxRun {BoxRunId}: completed batch {BatchNumber}.",
            boxRunId,
            batchNumber);

        return results.ToList();
    }

    private async Task RecoverStaleExecutionsAsync()
    {
        // 1. Abort interrupted task executions (they were killed mid-flight and cannot continue).
        const string reason = "Execution interrupted due to server restart.";
        var aborted = await _executionRepository.AbortRunningExecutionsAsync(DateTime.UtcNow, reason);
        _lastRecoveredExecutionCount = aborted;
        if (aborted > 0)
        {
            _logger.LogWarning(
                "Startup recovery: marked {Count} interrupted execution(s) as Aborted. Reason: {Reason}",
                aborted, reason);
        }

        // 2. Find BoxRuns that were interrupted (status = "Running") and enqueue them for resume.
        //    Their individual task executions were aborted above, so the DAG loop will retry them.
        var runningBoxRuns = await _boxRepository.GetRunningBoxRunsAsync();
        var resumedCount = 0;
        foreach (var boxRun in runningBoxRuns)
        {
            var enqueued = await _taskQueue.EnqueueAsync(new BoxRunRequest
            {
                BoxRunId = boxRun.BoxRunId,
                BoxId = boxRun.BoxId,
                RequestedAt = DateTime.UtcNow,
                TriggerSource = boxRun.TriggerSource,
                ScheduledForUtc = boxRun.ScheduledForUtc,
                RequestedByUserId = boxRun.RequestedByUserId,
                IsResume = true
            });

            if (enqueued)
            {
                resumedCount++;
                _logger.LogInformation(
                    "Startup recovery: enqueued interrupted BoxRun {BoxRunId} (Box {BoxId}) for resume.",
                    boxRun.BoxRunId, boxRun.BoxId);
            }
        }

        _lastRecoveredBoxRunCount = resumedCount;
        _lastRecoveryCompletedAtUtc = DateTime.UtcNow;
        _startupRecoveryCompleted = true;

        _logger.LogInformation(
            "Startup recovery completed. AbortedExecutions={AbortedExecutions}, ResumedBoxRuns={ResumedBoxRuns}, RecoveryCompletedAtUtc={RecoveryCompletedAtUtc}",
            _lastRecoveredExecutionCount,
            _lastRecoveredBoxRunCount,
            _lastRecoveryCompletedAtUtc);
    }

    private void MarkStartupRecoverySkipped()
    {
        _lastRecoveredExecutionCount = 0;
        _lastRecoveredBoxRunCount = 0;
        _lastRecoveryCompletedAtUtc = DateTime.UtcNow;
        _startupRecoveryCompleted = true;

        _logger.LogInformation(
            "Startup recovery was skipped because WorkerPool:AutoRecoverStaleExecutions is disabled.");
    }

    private void MarkStartupRecoveryDeferred(Exception exception)
    {
        _lastRecoveredExecutionCount = 0;
        _lastRecoveredBoxRunCount = 0;
        _lastRecoveryCompletedAtUtc = null;
        _startupRecoveryCompleted = false;

        _logger.LogError(
            exception,
            "Startup recovery failed. The worker pool will remain alive; recovery can complete after the database is reachable and the service is restarted.");
    }

    private async Task MarkBlockedTasksAsNotExecutedAsync(List<TaskDefinition> blockedTasks, BoxRunRequest request, string failureReason)
    {
        var now = DateTime.UtcNow;
        foreach (var task in blockedTasks)
        {
            // Create a NotExecuted execution record directly (not going through ExecuteTaskAsync since there's no actual execution).
            // This is the ONLY exception where we call the repository directly.
            await ((TaskExecutionService)_taskExecutionService).CreateNotExecutedExecutionAsync(
                task.Id,
                request.BoxRunId,
                now,
                request.TriggerSource,
                request.ScheduledForUtc,
                request.RequestedByUserId,
                failureReason);
        }
    }

    private static bool TryDetectDependencyCycle(
        IReadOnlyDictionary<int, List<TaskDependency>> dependenciesByTaskId,
        out string cyclePath)
    {
        var detectedCyclePath = string.Empty;
        var visitState = new Dictionary<int, int>(); // 0=unvisited, 1=visiting, 2=visited
        var recursionPath = new List<int>();
        var pathIndexByTaskId = new Dictionary<int, int>();

        bool Dfs(int taskId)
        {
            if (visitState.TryGetValue(taskId, out var state))
            {
                if (state == 1)
                {
                    var startIndex = pathIndexByTaskId[taskId];
                    var cycleNodes = recursionPath.Skip(startIndex).Concat(new[] { taskId });
                    detectedCyclePath = string.Join(" -> ", cycleNodes);
                    return true;
                }

                if (state == 2)
                    return false;
            }

            visitState[taskId] = 1;
            pathIndexByTaskId[taskId] = recursionPath.Count;
            recursionPath.Add(taskId);

            foreach (var dep in dependenciesByTaskId[taskId].OrderBy(d => d.DependsOnTaskId))
            {
                // Skip dangling references here; unresolved dependencies are handled by no-progress logic.
                if (!dependenciesByTaskId.ContainsKey(dep.DependsOnTaskId))
                    continue;

                if (Dfs(dep.DependsOnTaskId))
                    return true;
            }

            recursionPath.RemoveAt(recursionPath.Count - 1);
            pathIndexByTaskId.Remove(taskId);
            visitState[taskId] = 2;
            return false;
        }

        foreach (var taskId in dependenciesByTaskId.Keys.OrderBy(id => id))
        {
            if (visitState.TryGetValue(taskId, out var state) && state == 2)
                continue;

            if (Dfs(taskId))
            {
                cyclePath = detectedCyclePath;
                return true;
            }
        }

        cyclePath = string.Empty;
        return false;
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _stoppingCts?.Cancel();
        try { await Task.WhenAll(_workerTasks); }
        catch (OperationCanceledException) { }
        await base.StopAsync(cancellationToken);
    }

    // IWorkerStateService implementation
    public bool IsTaskRunning(int taskId) => ((TaskExecutionService)_taskExecutionService).IsTaskRunning(taskId);
    public int ActiveWorkerCount => ((TaskExecutionService)_taskExecutionService).ActiveCount;
    public int TotalWorkerCount => _workerCount;
    public int RunningBoxRunCount => _runningBoxRunIds.Count;
    public DateTime? LastRecoveryCompletedAtUtc => _lastRecoveryCompletedAtUtc;
    public int LastRecoveredExecutionCount => _lastRecoveredExecutionCount;
    public int LastRecoveredBoxRunCount => _lastRecoveredBoxRunCount;
    public bool StartupRecoveryCompleted => _startupRecoveryCompleted;
}
