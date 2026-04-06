namespace CHRONIQ.Domain;

/// <summary>
/// Read model for operational views listing BoxRun executions with box metadata.
/// </summary>
public class BoxRunSummary
{
    public int BoxRunId { get; set; }
    public int BoxId { get; set; }
    public string BoxName { get; set; } = "";
    public string Status { get; set; } = "Pending";
    public bool IsCancelled { get; set; }
    public DateTime? ScheduledForUtc { get; set; }
    public DateTime? StartedAtUtc { get; set; }
    public DateTime? EndedAtUtc { get; set; }
    public string TriggerSource { get; set; } = TriggerSources.Scheduler;
    public int? RequestedByUserId { get; set; }
    public DateTime CreatedAtUtc { get; set; }

    public TimeSpan? Duration =>
        StartedAtUtc.HasValue && EndedAtUtc.HasValue
            ? EndedAtUtc.Value - StartedAtUtc.Value
            : null;
}