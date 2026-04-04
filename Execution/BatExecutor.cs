using System.Diagnostics;
using AScheduler.Domain;

namespace AScheduler.Execution;

/// <summary>
/// Executor for running batch files (.bat) as scheduled tasks.
/// Wraps commands in cmd.exe with proper escaping and timeout management.
/// </summary>
public class BatExecutor : ITaskExecutor
{
    private readonly TimeSpan _timeout;

    /// <summary>
    /// Initializes a new instance of the BatExecutor class.
    /// </summary>
    /// <param name="timeout">The maximum duration for process execution. Defaults to 30 minutes if not specified.</param>
    public BatExecutor(TimeSpan? timeout = null)
    {
        _timeout = timeout ?? TimeSpan.FromMinutes(30);
    }

    /// <summary>
    /// Executes a batch command using cmd.exe.
    /// </summary>
    /// <param name="task">The task definition containing the batch command to execute.</param>
    /// <returns>The execution result containing output, error, and exit code.</returns>
    /// <exception cref="ArgumentNullException">Thrown when task is null.</exception>
    public async Task<ExecutionResult> ExecuteAsync(TaskDefinition task, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(task);
        using var timeoutCts = new CancellationTokenSource(_timeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c \"{task.Command}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        try
        {
            process.Start();
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            return new ExecutionResult
            {
                Output = string.Empty,
                Error = $"Failed to start cmd.exe: {ex.Message} (OS error {ex.NativeErrorCode}).",
                ExitCode = -1
            };
        }
        catch (UnauthorizedAccessException ex)
        {
            return new ExecutionResult
            {
                Output = string.Empty,
                Error = $"Access denied launching cmd.exe: {ex.Message}",
                ExitCode = -1
            };
        }

        try
        {
            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask  = process.StandardError.ReadToEndAsync();

            await Task.WhenAll(
                outputTask,
                errorTask,
                process.WaitForExitAsync(linkedCts.Token)
            );

            return new ExecutionResult
            {
                Output   = outputTask.Result,
                Error    = errorTask.Result,
                ExitCode = process.ExitCode
            };
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(entireProcessTree: true); } catch { }

            return new ExecutionResult
            {
                Output = "",
                Error = $"Process timed out after {_timeout.TotalMinutes} minutes.",
                ExitCode = -1
            };
        }
    }
}