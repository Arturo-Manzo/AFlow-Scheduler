namespace AScheduler.Domain
{
    /// <summary>
    /// Represents a concrete execution instance of a Box, triggered either
    /// by the scheduler (cron-based) or manually by a user.
    /// </summary>
    public class BoxRun
    {
        public int BoxRunId { get; set; }
        public int BoxId { get; set; }
        public DateTime? ScheduledForUtc { get; set; }
        public DateTime? StartedAtUtc { get; set; }
        public DateTime? EndedAtUtc { get; set; }
        public string Status { get; set; } = "Pending";
        public string TriggerSource { get; set; } = "Scheduled";
        public int? RequestedByUserId { get; set; }
        public DateTime CreatedAtUtc { get; set; }
    }
}
