namespace CHRONIQ.Domain;

/// <summary>
/// Represents a department or organizational unit within the system.
/// Departments provide governance boundaries, retry policies, and organizational structure.
/// </summary>
public class Department
{
    /// <summary>
    /// Unique identifier for the department.
    /// </summary>
    public int DepartmentId { get; set; }

    /// <summary>
    /// Department name. Must be unique within the system.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Optional description of the department's purpose or scope.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Contact email used as a reference owner for this department.
    /// This is informational and independent from task failure notifications.
    /// </summary>
    public string ContactEmail { get; set; } = string.Empty;

    /// <summary>
    /// Retry policy governing how tasks in this department can be retried.
    /// Values: "require-approval", "auto", "manual-only"
    /// </summary>
    public RetryPolicy RetryPolicy { get; set; } = RetryPolicy.RequireApproval;

    /// <summary>
    /// Number of days to retain task execution logs for this department.
    /// Compliance or archival policies may enforce this.
    /// </summary>
    public int LogRetentionDays { get; set; } = 90;

    /// <summary>
    /// UTC timestamp when the department was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// UTC timestamp of last update to the department.
    /// </summary>
    public DateTime? UpdatedAt { get; set; }
}

/// <summary>
/// Enumeration of retry policies that can be assigned to a department.
/// </summary>
public enum RetryPolicy
{
    /// <summary>
    /// Retries require explicit approval from an authorized administrator.
    /// All retry attempts are manually triggered and logged.
    /// </summary>
    RequireApproval = 0,

    /// <summary>
    /// Retries can be executed automatically or manually without additional approval.
    /// Still logged for audit purposes.
    /// </summary>
    Auto = 1,

    /// <summary>
    /// Only manual retries are allowed; no automatic retry mechanisms enabled.
    /// </summary>
    ManualOnly = 2
}
