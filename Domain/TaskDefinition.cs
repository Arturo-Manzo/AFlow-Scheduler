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
        // AllowParallel: reserved for future per-task parallelism control; not yet enforced by the worker.
        public bool AllowParallel { get; set; }
        // SortOrder: UI display hint only; execution order is determined solely by dependency graph.
        public int SortOrder { get; set; }
        public bool Enabled { get; set; }
        public DateTime CreatedAtUtc { get; set; }
    }
}
