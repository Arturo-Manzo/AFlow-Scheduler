using AScheduler.Domain;

namespace AScheduler.Queue;

public interface ITaskQueue
{
    Task<bool> EnqueueAsync(BoxRunRequest request);
    Task<BoxRunRequest> DequeueAsync(CancellationToken ct);
}
