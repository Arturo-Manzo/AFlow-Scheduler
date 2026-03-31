using System.Collections.Concurrent;
using AScheduler.Data;
using AScheduler.Domain;
using AScheduler.Execution;
using AScheduler.Queue;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AScheduler.Services;

public class ConfigurableWorkerPool : BackgroundService, IWorkerStateService
{
    private readonly IBoxRepository _boxRepository;
    private readonly ITaskRepository _taskRepository;
    private readonly IExecutionRepository _executionRepository;
    private readonly ITaskQueue _taskQueue;
    private readonly ExecutorFactory _executorFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ConfigurableWorkerPool> _logger;

    // Guards against the same BoxRun being picked up by two workers simultaneously (single-process safety only).
    private readonly ConcurrentDictionary<int, byte> _runningBoxRunIds = new();

    // Guards against concurrent execution of two different BoxRuns for the same Box.
    // LIMITATION: This is in-memory only. If the application runs as multiple instances
    // (horizontal scaling), the same BoxId can execute concurrently across instances.
    // Distributed locking (e.g. DB-level status check or Redis) is required for that scenario.
    private readonly ConcurrentDictionary<int, byte> _runningBoxIds = new();

    // Guards against concurrent force-start execution of the same Task (single-process only).
    private readonly ConcurrentDictionary<int, byte> _runningTaskIds = new();

    private int _workerCount;
    private int _taskTimeoutSeconds;
    private List<Task> _workerTasks = new();
    private CancellationTokenSource? _stoppingCts;

    public ConfigurableWorkerPool(
        IBoxRepository boxRepository,
        ITaskRepository taskRepository,
        IExecutionRepository executionRepository,
        ITaskQueue taskQueue,
        ExecutorFactory executorFactory,
        IConfiguration configuration,
        ILogger<ConfigurableWorkerPool> logger)
    {
        ArgumentNullException.ThrowIfNull(boxRepository);
        ArgumentNullException.ThrowIfNull(taskRepository);
        ArgumentNullException.ThrowIfNull(executionRepository);
        ArgumentNullException.ThrowIfNull(taskQueue);
        ArgumentNullException.ThrowIfNull(executorFactory);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(logger);

        _boxRepository = boxRepository;
        _taskRepository = taskRepository;
        _executionRepository = executionRepository;
        _taskQueue = taskQueue;
        _executorFactory = executorFactory;
        _configuration = configuration;
        _logger = logger;

        _workerCount = Math.Max(1, Math.Min(_configuration.GetValue<int>("WorkerPool:WorkerCount", 4), 20));
        _taskTimeoutSeconds = Math.Max(10, _configuration.GetValue<int>("WorkerPool:TaskTimeoutSeconds", 300));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _stoppingCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        _logger.LogInformation("Worker pool starting with {WorkerCount} workers.", _workerCount);

        try
        {
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
            await _boxRepository.UpdateBoxRunStatusAsync(boxRunId, "Running", DateTime.UtcNow, null);
            _logger.LogInformation("Worker {WorkerId} executing BoxRun {BoxRunId} for Box {BoxId}.", workerId, boxRunId, boxId);

            var tasks = await _taskRepository.GetTasksForBoxAsync(boxId);
            if (tasks.Count == 0)
            {
                _logger.LogWarning("BoxRun {BoxRunId}: no tasks found for Box {BoxId}.", boxRunId, boxId);
                await FinalizeBoxRun(boxRunId, boxId, "Success");
                return;
            }

            var anyFailed = false;
            var anyBlocked = false;
            var taskById = tasks.ToDictionary(t => t.Id);
            var pendingTaskIds = taskById.Keys.ToHashSet();
            var completedSuccess = new HashSet<int>();
            var waitingLogged = new HashSet<int>();

            var dependenciesByTaskId = new Dictionary<int, List<int>>();
            foreach (var taskId in taskById.Keys)
                dependenciesByTaskId[taskId] = await _taskRepository.GetTaskDependenciesAsync(taskId);

            while (pendingTaskIds.Count > 0)
            {
                List<int> readyTaskIds;
                if (request.ForceIgnoreDependencies)
                {
                    readyTaskIds = pendingTaskIds.ToList();
                }
                else
                {
                    readyTaskIds = pendingTaskIds
                        .Where(taskId => dependenciesByTaskId[taskId].All(depId => completedSuccess.Contains(depId)))
                        .ToList();
                }

                var waitingTaskIds = pendingTaskIds.Where(id => !readyTaskIds.Contains(id)).ToList();
                foreach (var waitingTaskId in waitingTaskIds)
                {
                    if (waitingLogged.Add(waitingTaskId))
                    {
                        await SaveWaitingStatusAsync(taskById[waitingTaskId], boxRunId, request);
                    }
                }

                if (readyTaskIds.Count == 0)
                {
                    anyBlocked = true;
                    _logger.LogWarning("BoxRun {BoxRunId}: unresolved dependency graph. Remaining tasks: {RemainingTaskIds}", boxRunId, string.Join(',', pendingTaskIds));
                    break;
                }

                var executionTasks = readyTaskIds.Select(taskId => ExecuteTaskAsync(taskById[taskId], boxRunId, request, ct)).ToList();
                var results = await Task.WhenAll(executionTasks);

                foreach (var result in results)
                {
                    pendingTaskIds.Remove(result.TaskId);

                    if (result.Success)
                    {
                        completedSuccess.Add(result.TaskId);
                    }
                    else
                    {
                        anyFailed = true;
                    }
                }
            }

            var finalStatus = anyFailed
                ? (anyBlocked ? "Partial" : "Failed")
                : (anyBlocked ? "Partial" : "Success");

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
        await _boxRepository.UpdateBoxRunStatusAsync(boxRunId, status, null, endTime);
        await _boxRepository.UpdateLastRunAsync(boxId, endTime);
        _logger.LogInformation("BoxRun {BoxRunId} finalized with status {Status}.", boxRunId, status);
    }

    // -------------------------------------------------------------------------
    // ISOLATED TASK FORCE-START PATH
    // Completely separate from BoxRun execution. No BoxRun is created or updated.
    // -------------------------------------------------------------------------

    private async Task ExecuteTaskForceStartAsync(int workerId, TaskForceStartRequest request, CancellationToken ct)
    {
        var taskId = request.TaskId;

        // Reject if the same task is already running — no requeue, caller gets a 409 at enqueue time
        // for in-queue duplicates; this guard catches the case where execution is already in progress.
        if (!_runningTaskIds.TryAdd(taskId, 0))
        {
            _logger.LogWarning("Task {TaskId} is already executing. Force-start rejected.", taskId);
            return;
        }

        try
        {
            var task = await _taskRepository.GetByIdAsync(taskId);
            if (task == null)
            {
                _logger.LogError("ForceStart: Task {TaskId} not found at execution time.", taskId);
                return;
            }

            _logger.LogInformation("Worker {WorkerId} executing force-start for Task {TaskId} ({TaskName}).",
                workerId, taskId, task.Name);

            var startTime = DateTime.UtcNow;
            var executor = _executorFactory.GetExecutor(task.TaskType);

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(_taskTimeoutSeconds));

            try
            {
                var result = await executor.ExecuteAsync(task);
                var endTime = DateTime.UtcNow;
                var status = result.ExitCode == 0 ? "Success" : "Failed";

                await _executionRepository.SaveDirectExecutionAsync(
                    task.Id, startTime, endTime, status,
                    result.Output, result.Error, result.ExitCode,
                    result.Output, result.Error,
                    request.RequestedByUserId, request.Reason);

                _logger.LogInformation("ForceStart Task {TaskId} finished with status {Status} (exit code {ExitCode}).",
                    taskId, status, result.ExitCode);
            }
            catch (OperationCanceledException)
            {
                await _executionRepository.SaveDirectExecutionAsync(
                    task.Id, startTime, DateTime.UtcNow, "Timeout",
                    "", $"Timeout after {_taskTimeoutSeconds}s", -1,
                    "", $"Timeout after {_taskTimeoutSeconds}s",
                    request.RequestedByUserId, request.Reason);

                _logger.LogWarning("ForceStart Task {TaskId} timed out after {Seconds}s.", taskId, _taskTimeoutSeconds);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ForceStart Task {TaskId} fatal error.", taskId);
        }
        finally
        {
            _runningTaskIds.TryRemove(taskId, out _);
        }
    }

    private async Task SaveWaitingStatusAsync(TaskDefinition task, int boxRunId, BoxRunRequest request)
    {
        var now = DateTime.UtcNow;
        await _executionRepository.SaveExecutionAsync(
            task.Id, boxRunId,
            now, now, "Waiting",
            "", "Waiting for dependencies to complete successfully.", -2,
            "", "Waiting for dependencies to complete successfully.",
            request.TriggerSource, request.ScheduledForUtc);
    }

    private async Task<(int TaskId, bool Success)> ExecuteTaskAsync(TaskDefinition task, int boxRunId, BoxRunRequest request, CancellationToken ct)
    {
        var startTime = DateTime.UtcNow;
        var executor = _executorFactory.GetExecutor(task.TaskType);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(_taskTimeoutSeconds));

        try
        {
            var result = await executor.ExecuteAsync(task);
            var endTime = DateTime.UtcNow;
            var status = result.ExitCode == 0 ? "Success" : "Failed";

            await _executionRepository.SaveExecutionAsync(
                task.Id, boxRunId, startTime, endTime, status,
                result.Output, result.Error, result.ExitCode,
                result.Output, result.Error,
                request.TriggerSource, request.ScheduledForUtc);

            _logger.LogInformation("BoxRun {BoxRunId} Task {TaskId} finished with exit code {ExitCode}.", boxRunId, task.Id, result.ExitCode);
            return (task.Id, result.ExitCode == 0);
        }
        catch (OperationCanceledException)
        {
            await _executionRepository.SaveExecutionAsync(
                task.Id, boxRunId,
                startTime, DateTime.UtcNow, "Timeout",
                "", $"Timeout after {_taskTimeoutSeconds}s", -1,
                "", $"Timeout after {_taskTimeoutSeconds}s",
                request.TriggerSource, request.ScheduledForUtc);

            _logger.LogWarning("BoxRun {BoxRunId} Task {TaskId} timed out.", boxRunId, task.Id);
            return (task.Id, false);
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _stoppingCts?.Cancel();
        try { await Task.WhenAll(_workerTasks); }
        catch (OperationCanceledException) { }
        await base.StopAsync(cancellationToken);
    }

    // IWorkerStateService implementation
    public bool IsTaskRunning(int taskId) => _runningTaskIds.ContainsKey(taskId);
}
