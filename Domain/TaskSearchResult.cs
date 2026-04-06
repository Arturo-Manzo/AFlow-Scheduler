namespace CHRONIQ.Domain;

/// <summary>
/// Lightweight projection used to locate tasks across all boxes without loading full box detail.
/// </summary>
public class TaskSearchResult
{
    /// <summary>
    /// Task identifier.
    /// </summary>
    public int TaskId { get; set; }

    /// <summary>
    /// Parent box identifier.
    /// </summary>
    public int BoxId { get; set; }

    /// <summary>
    /// Task display name.
    /// </summary>
    public string TaskName { get; set; } = "";

    /// <summary>
    /// Task description.
    /// </summary>
    public string TaskDescription { get; set; } = "";

    /// <summary>
    /// Task command string.
    /// </summary>
    public string Command { get; set; } = "";

    /// <summary>
    /// Task type name.
    /// </summary>
    public string TaskType { get; set; } = "";

    /// <summary>
    /// Indicates whether the task is enabled.
    /// </summary>
    public bool TaskEnabled { get; set; }

    /// <summary>
    /// Task creation timestamp in UTC.
    /// </summary>
    public DateTime CreatedAtUtc { get; set; }

    /// <summary>
    /// Parent box display name.
    /// </summary>
    public string BoxName { get; set; } = "";

    /// <summary>
    /// Parent box description.
    /// </summary>
    public string BoxDescription { get; set; } = "";

    /// <summary>
    /// Indicates whether the parent box is enabled.
    /// </summary>
    public bool BoxEnabled { get; set; }
}