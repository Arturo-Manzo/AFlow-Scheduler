using CHRONIQ.Domain;

namespace CHRONIQ.Execution
{
    /// <summary>
    /// Defines the contract for task execution implementations.
    /// </summary>
    public interface ITaskExecutor
    {
        /// <summary>
        /// Executes a task and returns the execution result.
        /// </summary>
        /// <param name="task">The task definition to execute.</param>
        /// <param name="cancellationToken">Cancellation token for cooperative cancellation.</param>
        /// <returns>The execution result containing output, error streams, and exit code.</returns>
        Task<ExecutionResult> ExecuteAsync(TaskDefinition task, CancellationToken cancellationToken);
    }

    /// <summary>
    /// Contains the results of a task execution including output, errors, and exit code.
    /// </summary>
    public class ExecutionResult
    {
        /// <summary>
        /// Gets or sets the standard output produced by the executed process.
        /// </summary>
        public string Output { get; set; } = "";

        /// <summary>
        /// Gets or sets the standard error output produced by the executed process.
        /// </summary>
        public string Error { get; set; } = "";

        /// <summary>
        /// Gets or sets the process exit code. 0 typically indicates success.
        /// </summary>
        public int ExitCode { get; set; }
    }
}