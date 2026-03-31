namespace AScheduler.Services;

/// <summary>
/// Provides read-only access to the worker pool's in-flight execution state.
/// Used by controllers to give fast, API-level responses before items enter the queue.
///
/// NOTE: This reflects in-process state only. In a multi-instance deployment,
/// a task that is "not running" here may be running on another instance.
/// </summary>
public interface IWorkerStateService
{
    /// <summary>Returns true if a force-start execution for this TaskId is currently in progress.</summary>
    bool IsTaskRunning(int taskId);
}
