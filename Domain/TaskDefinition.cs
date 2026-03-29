namespace AScheduler.Domain
{
    /// <summary>
    /// Represents a Task step: an executable unit within a Box.
    /// CronExpression and scheduling now live on BoxDefinition.
    /// </summary>
    public class TaskDefinition
    {
        public int Id { get; set; }
        public int BoxId { get; set; }
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public string Command { get; set; } = "";
        public TaskType TaskType { get; set; }
        public bool AllowParallel { get; set; }
        public int SortOrder { get; set; }
        public bool Enabled { get; set; }
        public DateTime CreatedAtUtc { get; set; }
    }
}
