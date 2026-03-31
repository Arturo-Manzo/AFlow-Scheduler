namespace AScheduler.Domain
{
    /// <summary>
    /// A request to execute a Box run, placed in the in-memory queue.
    /// Contains the BoxRunId that was already persisted in the DB before enqueueing.
    /// </summary>
    public class BoxRunRequest
    {
        public int BoxRunId { get; set; }
        public int BoxId { get; set; }
        public DateTime RequestedAt { get; set; }
        /// <summary>
        /// Manual override: skip dependency-graph evaluation and execute all tasks unconditionally.
        /// Only ever set to true for manual (non-scheduled) runs.
        /// </summary>
        public bool ForceIgnoreDependencies { get; set; }
        /// <summary>
        /// Informational flag carried from the BoxExecutionQueue.IgnoreSchedule column.
        /// The worker does not consume this — manual runs bypass the schedule by design.
        /// Reserved for audit / future use.
        /// </summary>
        public bool ForceIgnoreSchedule { get; set; }
        public int? RequestedByUserId { get; set; }
        public string TriggerSource { get; set; } = "Scheduled";
        public DateTime? ScheduledForUtc { get; set; }
    }
}
