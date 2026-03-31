namespace AScheduler.Domain
{
    /// <summary>
    /// Represents a concrete execution instance of a Box, triggered either
    /// by the scheduler (cron-based) or manually by a user.
    /// 
    /// Lifecycle States:
    /// - Pending: BoxRun created but not yet started (initial state)
    /// - Running: Execution is in progress (at least one task is executing or ready)
    /// - Completed: All tasks finished successfully (all with exit code 0)
    /// - Partial: All tasks finished but some failed (mixed success/failure)
    /// - Failed: All tasks finished but none succeeded, or a fatal error prevented successful completion
    /// - Cancelled: Execution was stopped before all pending tasks could be scheduled
    /// 
    /// State Transitions:
    /// Pending → Running → Completed | Partial | Failed | Cancelled
    /// (No backward transitions; once complete, never rerun from same BoxRun instance)
    /// </summary>
    public class BoxRun
    {
        /// <summary>
        /// Unique identifier for this BoxRun instance.
        /// </summary>
        public int BoxRunId { get; set; }

        /// <summary>
        /// The Box this run belongs to.
        /// </summary>
        public int BoxId { get; set; }

        /// <summary>
        /// The UTC time this execution was scheduled for (Cron-derived time, or manually set).
        /// May be null if run was triggered immediately with no schedule.
        /// </summary>
        public DateTime? ScheduledForUtc { get; set; }

        /// <summary>
        /// When the execution actually started (transitioned to Running).
        /// Null while Pending, set during execution start.
        /// </summary>
        public DateTime? StartedAtUtc { get; set; }

        /// <summary>
        /// When the execution completed (transitioned from Running to final state).
        /// Null while Pending/Running, set when entire BoxRun is done.
        /// </summary>
        public DateTime? EndedAtUtc { get; set; }

        /// <summary>
        /// Current lifecycle state: Pending | Running | Completed | Partial | Failed | Cancelled
        /// </summary>
        public string Status { get; set; } = "Pending";

        /// <summary>
        /// Cancellation request flag. Running tasks are allowed to finish; the worker only
        /// stops before scheduling further pending tasks.
        /// </summary>
        public bool IsCancelled { get; set; }

        public TimeSpan? Duration =>
            StartedAtUtc.HasValue && EndedAtUtc.HasValue
                ? EndedAtUtc.Value - StartedAtUtc.Value
                : null;

        /// <summary>
        /// Source of the trigger: Scheduler | Manual | ForceStart | Retry
        /// </summary>
        public string TriggerSource { get; set; } = TriggerSources.Scheduler;

        /// <summary>
        /// User ID of the person who manually triggered this BoxRun (if applicable).
        /// Null for scheduled executions.
        /// </summary>
        public int? RequestedByUserId { get; set; }

        /// <summary>
        /// When this BoxRun record was created in the database.
        /// </summary>
        public DateTime CreatedAtUtc { get; set; }
    }
}
