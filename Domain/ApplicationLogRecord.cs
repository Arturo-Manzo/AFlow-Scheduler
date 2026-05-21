namespace CHRONIQ.Domain;

public sealed class ApplicationLogRecord
{
    public long Id { get; set; }
    public string LogFileName { get; set; } = "";
    public DateTime Timestamp { get; set; }
    public string Level { get; set; } = "";
    public string Message { get; set; } = "";
    public string? ErrorFile { get; set; }
    public string? ErrorMethod { get; set; }
    public int? ErrorLine { get; set; }
    public string? ExceptionType { get; set; }
    public string? Source { get; set; }
    public Guid? CorrelationId { get; set; }
    public int? UserId { get; set; }
    public string? RequestPath { get; set; }
    public int? StatusCode { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class ApplicationLogSummary
{
    public int ErrorCount { get; set; }
    public int WarningCount { get; set; }
}
