using CHRONIQ.Domain;

namespace CHRONIQ.Queue;

/// <summary>
/// Discriminated union representing a unit of work placed in the worker queue.
/// Workers switch on the concrete type to dispatch to the correct execution path.
/// </summary>
public abstract class WorkerItem { }

/// <summary>Wraps a full BoxRun execution request (scheduled or manual via BoxExecutionQueue).</summary>
public sealed class BoxRunItem : WorkerItem
{
    public BoxRunRequest Request { get; }
    public BoxRunItem(BoxRunRequest request) => Request = request;
}

/// <summary>Wraps an isolated single-task force-start request (manual, no BoxRun).</summary>
public sealed class TaskForceStartItem : WorkerItem
{
    public TaskForceStartRequest Request { get; }
    public TaskForceStartItem(TaskForceStartRequest request) => Request = request;
}
