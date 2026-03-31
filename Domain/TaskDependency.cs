namespace AScheduler.Domain
{
    /// <summary>
    /// Represents a dependency edge in a task graph within a box.
    /// TaskId depends on DependsOnTaskId.
    /// </summary>
    public class TaskDependency
    {
        public int TaskId { get; set; }
        public int DependsOnTaskId { get; set; }
    }
}