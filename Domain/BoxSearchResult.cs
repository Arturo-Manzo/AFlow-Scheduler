namespace AScheduler.Domain;

/// <summary>
/// Lightweight projection used to locate boxes from a global search flow.
/// </summary>
public class BoxSearchResult
{
    /// <summary>
    /// Box identifier.
    /// </summary>
    public int BoxId { get; set; }

    /// <summary>
    /// Box display name.
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    /// Box description.
    /// </summary>
    public string Description { get; set; } = "";

    /// <summary>
    /// Box time zone identifier.
    /// </summary>
    public string TimeZoneId { get; set; } = "Etc/UTC";

    /// <summary>
    /// Whether the box is enabled.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Box creation timestamp in UTC.
    /// </summary>
    public DateTime CreatedAtUtc { get; set; }

    /// <summary>
    /// Number of active tasks currently attached to the box.
    /// </summary>
    public int ActiveTaskCount { get; set; }
}