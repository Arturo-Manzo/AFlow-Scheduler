using System.Threading.Channels;
using AScheduler.Domain;
using Microsoft.Extensions.Logging;

namespace AScheduler.Queue;

/// <summary>
/// Unified in-memory queue for both BoxRun requests and TaskForceStart requests.
/// Provides duplicate prevention independently for each type.
///
/// CONCURRENCY NOTE: This queue is in-memory and single-process only.
/// Running multiple application instances will result in separate queues with no coordination.
/// </summary>
public class TaskQueue : ITaskQueue
{
    private readonly Channel<WorkerItem> _channel;
    private readonly HashSet<int> _enqueuedBoxRunIds = new();
    private readonly HashSet<int> _enqueuedForceStartTaskIds = new();
    private readonly object _lock = new();
    private readonly ILogger<TaskQueue> _logger;

    public TaskQueue(ILogger<TaskQueue> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
        _channel = Channel.CreateUnbounded<WorkerItem>();
    }

    public async Task<bool> EnqueueAsync(BoxRunRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        lock (_lock)
        {
            if (_enqueuedBoxRunIds.Contains(request.BoxRunId))
            {
                _logger.LogWarning("BoxRun {BoxRunId} already pending in queue.", request.BoxRunId);
                return false;
            }
            _enqueuedBoxRunIds.Add(request.BoxRunId);
        }

        try
        {
            await _channel.Writer.WriteAsync(new BoxRunItem(request));
            _logger.LogInformation("BoxRun {BoxRunId} enqueued.", request.BoxRunId);
            return true;
        }
        catch (ChannelClosedException ex)
        {
            lock (_lock) { _enqueuedBoxRunIds.Remove(request.BoxRunId); }
            _logger.LogError(ex, "Failed to enqueue BoxRun {BoxRunId}: channel closed.", request.BoxRunId);
            throw;
        }
    }

    public async Task<bool> EnqueueForceStartAsync(TaskForceStartRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        lock (_lock)
        {
            if (_enqueuedForceStartTaskIds.Contains(request.TaskId))
            {
                _logger.LogWarning("Task {TaskId} already pending for force-start.", request.TaskId);
                return false;
            }
            _enqueuedForceStartTaskIds.Add(request.TaskId);
        }

        try
        {
            await _channel.Writer.WriteAsync(new TaskForceStartItem(request));
            _logger.LogInformation("Task {TaskId} enqueued for force-start.", request.TaskId);
            return true;
        }
        catch (ChannelClosedException ex)
        {
            lock (_lock) { _enqueuedForceStartTaskIds.Remove(request.TaskId); }
            _logger.LogError(ex, "Failed to enqueue force-start for Task {TaskId}: channel closed.", request.TaskId);
            throw;
        }
    }

    public int QueueDepth => _channel.Reader.Count;

    public async Task<WorkerItem> DequeueAsync(CancellationToken ct)
    {
        var item = await _channel.Reader.ReadAsync(ct);

        lock (_lock)
        {
            if (item is BoxRunItem boxRunItem)
                _enqueuedBoxRunIds.Remove(boxRunItem.Request.BoxRunId);
            else if (item is TaskForceStartItem forceStartItem)
                _enqueuedForceStartTaskIds.Remove(forceStartItem.Request.TaskId);
        }

        return item;
    }
}
