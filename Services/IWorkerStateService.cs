namespace CHRONIQ.Services;

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

    /// <summary>Number of worker tasks currently processing an item.</summary>
    int ActiveWorkerCount { get; }

    /// <summary>Total number of worker tasks configured in the pool.</summary>
    int TotalWorkerCount { get; }

    /// <summary>Number of BoxRuns currently being executed in this process.</summary>
    int RunningBoxRunCount { get; }

    /// <summary>UTC timestamp when startup recovery last completed.</summary>
    DateTime? LastRecoveryCompletedAtUtc { get; }

    /// <summary>Count of interrupted task executions marked as Aborted during last startup recovery.</summary>
    int LastRecoveredExecutionCount { get; }

    /// <summary>Count of interrupted BoxRuns re-enqueued during last startup recovery.</summary>
    int LastRecoveredBoxRunCount { get; }

    /// <summary>Whether startup recovery has completed at least once since process start.</summary>
    bool StartupRecoveryCompleted { get; }
}
