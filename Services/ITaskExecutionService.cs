namespace CHRONIQ.Services;

/// <summary>
/// Centralized service for executing individual tasks.
/// ALL task executions in the system MUST go through this service.
/// This is the single entry point that enforces:
/// - In-process concurrency control via _runningTaskIds guard
/// - Database constraint violations as cross-instance defense
/// - Proper lifecycle (create Running → complete with status/output)
/// - Consistent error handling and logging
/// 
/// DO NOT call IExecutionRepository.CreateExecutionAsync directly.
/// This service is the ONLY authorized caller.
/// </summary>
public interface ITaskExecutionService
{
    /// <summary>
    /// Execute a single task with full concurrency protection and lifecycle management.
    /// Returns true if execution succeeded (exit code 0), false otherwise.
    /// 
    /// This method:
    /// 1. Guards against in-process duplicate execution via _runningTaskIds
    /// 2. Attempts to create an execution record in the database (status='Running')
    /// 3. Catches DB constraint violations as cross-instance protection
    /// 4. Executes the task via the appropriate executor
    /// 5. Completes the execution record with final status and output
    /// 6. Cleans up the in-process guard
    /// 
    /// Execution paths:
    /// - ForceStart: boxRunId=null, triggerSource="ForceStart"
    /// - BoxRun flow: boxRunId!=null, triggerSource="Scheduler"/"Manual"/"Retry"
    /// - Blocked (dependency failure): creates Failed execution immediately
    /// </summary>
    Task<bool> ExecuteTaskAsync(
        CHRONIQ.Domain.TaskDefinition task,
        int? boxRunId,
        string triggerSource,
        DateTime? scheduledForUtc,
        int? requestedByUserId,
        string? reason,
        CancellationToken ct);
}
