namespace AScheduler.Domain;

/// <summary>
/// A request to execute a single task in isolation, without a BoxRun context.
/// Dependencies are unconditionally ignored — this is always a manual operation.
/// Does NOT create or interact with a BoxRun.
/// </summary>
public class TaskForceStartRequest
{
    public int TaskId { get; set; }
    public int? RequestedByUserId { get; set; }
    public string Reason { get; set; } = "";
    public DateTime RequestedAtUtc { get; set; }
}
