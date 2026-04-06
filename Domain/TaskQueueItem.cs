namespace CHRONIQ.Domain;

/// <summary>
/// Represents a row from the BoxExecutionQueue table — a manual run-now
/// request submitted via the API.
/// </summary>
public class BoxQueueItem
{
    public int QueueId { get; set; }
    public int BoxId { get; set; }
    public int? RequestedByUserId { get; set; }
    public bool IgnoreDependencies { get; set; }
    public bool IgnoreSchedule { get; set; }
    public string? Reason { get; set; }
    public string Status { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}
