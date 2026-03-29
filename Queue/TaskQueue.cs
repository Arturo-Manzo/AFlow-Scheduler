using System.Threading.Channels;
using AScheduler.Domain;
using Microsoft.Extensions.Logging;

namespace AScheduler.Queue;

/// <summary>
/// Manages a queue of task execution requests with duplicate prevention.
/// </summary>
public class TaskQueue : ITaskQueue
{
    private readonly Channel<BoxRunRequest> _queue;
    private readonly HashSet<int> _enqueuedBoxRunIds;
    private readonly object _lockObject = new();
    private readonly ILogger<TaskQueue> _logger;

    public TaskQueue(ILogger<TaskQueue> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
        _queue = Channel.CreateUnbounded<BoxRunRequest>();
        _enqueuedBoxRunIds = new HashSet<int>();
    }

    public async Task<bool> EnqueueAsync(BoxRunRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        lock (_lockObject)
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
            await _queue.Writer.WriteAsync(request);
            _logger.LogInformation("BoxRun {BoxRunId} enqueued.", request.BoxRunId);
            return true;
        }
        catch (ChannelClosedException ex)
        {
            lock (_lockObject)
            {
                _enqueuedBoxRunIds.Remove(request.BoxRunId);
            }
            _logger.LogError(ex, "Failed to enqueue BoxRun {BoxRunId}: queue closed.", request.BoxRunId);
            throw;
        }
    }

    /// <summary>
    /// Dequeues the next task execution request from the queue.
    /// </summary>
    /// <param name="ct">Cancellation token to cancel the dequeue operation.</param>
    /// <returns>The next task execution request from the queue.</returns>
    /// <exception cref="OperationCanceledException">Thrown when the cancellation token is triggered.</exception>
    public async Task<BoxRunRequest> DequeueAsync(CancellationToken ct)
    {
        var request = await _queue.Reader.ReadAsync(ct);

        lock (_lockObject)
        {
            _enqueuedBoxRunIds.Remove(request.BoxRunId);
        }

        _logger.LogInformation("BoxRun {BoxRunId} dequeued.", request.BoxRunId);
        return request;
    }
}