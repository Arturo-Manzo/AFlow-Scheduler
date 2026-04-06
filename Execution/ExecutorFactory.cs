using CHRONIQ.Domain;

namespace CHRONIQ.Execution
{
    /// <summary>
    /// Factory for creating appropriate task executors based on task type.
    /// Uses dependency injection to instantiate executor instances.
    /// </summary>
    public class ExecutorFactory
    {
        private readonly IServiceProvider _serviceProvider;

        /// <summary>
        /// Initializes a new instance of the ExecutorFactory class.
        /// </summary>
        /// <param name="serviceProvider">The service provider for resolving executor dependencies.</param>
        /// <exception cref="ArgumentNullException">Thrown when serviceProvider is null.</exception>
        public ExecutorFactory(IServiceProvider serviceProvider)
        {
            ArgumentNullException.ThrowIfNull(serviceProvider);
            _serviceProvider = serviceProvider;
        }

        /// <summary>
        /// Gets an executor instance for the specified task type.
        /// </summary>
        /// <param name="taskType">The type of task to execute.</param>
        /// <returns>An ITaskExecutor implementation appropriate for the task type.</returns>
        /// <exception cref="NotImplementedException">Thrown when no executor is available for the task type.</exception>
        public ITaskExecutor GetExecutor(TaskType taskType)
        {
            return taskType switch
            {
                TaskType.Exe => _serviceProvider.GetRequiredService<ExeExecutor>(),
                TaskType.Bat => _serviceProvider.GetRequiredService<BatExecutor>(),
                TaskType.Python => _serviceProvider.GetRequiredService<PythonExecutor>(),
                TaskType.Api => _serviceProvider.GetRequiredService<ApiExecutor>(),
                _ => throw new NotImplementedException($"No executor is implemented for task type '{taskType}'.")
            };
        }
    }
}