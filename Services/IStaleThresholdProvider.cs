namespace CHRONIQ.Services;

/// <summary>
/// Provides per-task dynamic stale execution thresholds based on historical average duration.
/// </summary>
public interface IStaleThresholdProvider
{
    /// <summary>
    /// Returns the stale threshold in minutes for the given task.
    /// Falls back to the global <c>WorkerPool:StaleExecutionThresholdMinutes</c> when no execution history exists.
    /// </summary>
    Task<double> GetStaleThresholdMinutesAsync(int taskId);

    /// <summary>
    /// Determines whether the given running execution is stale based on per-task dynamic thresholds.
    /// </summary>
    Task<bool> IsStaleAsync(int taskId, DateTime startedAtUtc);
}
