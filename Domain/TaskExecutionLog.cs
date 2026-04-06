namespace CHRONIQ.Domain;

public class TaskExecutionLog
{
    public Guid Id { get; set; }
    public int? BoxRunId { get; set; }
    public int TaskId { get; set; }
    public int TaskExecutionId { get; set; }
    public DateTime Timestamp { get; set; }
    public string Level { get; set; } = "Info";
    public string Message { get; set; } = "";
    public string? Details { get; set; }
}