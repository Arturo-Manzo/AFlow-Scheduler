using AScheduler.Domain;

namespace AScheduler.Queue;

public interface ITaskQueue
{
    /// <summary>Enqueue a full BoxRun request. Returns false if the same BoxRunId is already pending.</summary>
    Task<bool> EnqueueAsync(BoxRunRequest request);

    /// <summary>
    /// Enqueue an isolated task force-start request.
    /// Returns false if the same TaskId is already pending in the queue.
    /// </summary>
    Task<bool> EnqueueForceStartAsync(TaskForceStartRequest request);

    /// <summary>Dequeue the next work item (either BoxRunItem or TaskForceStartItem).</summary>
    Task<WorkerItem> DequeueAsync(CancellationToken ct);

    /// <summary>Number of items currently waiting in the queue (not yet dequeued by a worker).</summary>
    int QueueDepth { get; }
}
