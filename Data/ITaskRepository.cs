using AScheduler.Domain;

namespace AScheduler.Data
{
    public interface ITaskRepository
    {
        Task<List<TaskDefinition>> GetTasksForBoxAsync(int boxId);
        Task<TaskDefinition?> GetByIdAsync(int taskId);
        Task<List<TaskSearchResult>> SearchAsync(string query, int limit);
        Task<int> CreateAsync(int boxId, string name, string description, string command, string taskType);
        Task<bool> UpdateAsync(int taskId, string name, string description, string command, string taskType, bool enabled);
        Task<bool> DeleteAsync(int taskId);
        Task<List<int>> GetTaskDependenciesAsync(int taskId);
        Task ReplaceTaskDependenciesAsync(int taskId, IEnumerable<int> dependencyTaskIds);
    }
}
