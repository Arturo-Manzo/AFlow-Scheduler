namespace AScheduler.Domain
{
    /// <summary>
    /// Represents a Box: a scheduling container that owns the CronExpression
    /// and groups one or more Task steps into a single execution unit.
    /// </summary>
    public class BoxDefinition
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public string CronExpression { get; set; } = "";
        public string TimeZoneId { get; set; } = "Etc/UTC";
        public bool AllowParallel { get; set; }
        public bool Enabled { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public DateTime? LastRunUtc { get; set; }
        /// <summary>
        /// Optional email address to notify when tasks in this box fail.
        /// </summary>
        public string? NotificationEmail { get; set; }
        /// <summary>
        /// Department ID that owns this box. Controls visibility and governance.
        /// </summary>
        public int? DepartmentId { get; set; }
        /// <summary>
        /// Name of the department that owns this box, resolved via JOIN.
        /// </summary>
        public string? DepartmentName { get; set; }
    }
}
