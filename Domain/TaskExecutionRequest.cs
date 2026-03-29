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
        public bool ForceIgnoreDependencies { get; set; }
        public bool ForceIgnoreSchedule { get; set; }
        public int? RequestedByUserId { get; set; }
        public string TriggerSource { get; set; } = "Scheduled";
        public DateTime? ScheduledForUtc { get; set; }
    }
}
