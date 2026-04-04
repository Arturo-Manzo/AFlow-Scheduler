using System;
using System.Diagnostics;
using AScheduler.Domain;
using System.Threading;
using System.Threading.Tasks;

namespace AScheduler.Execution;

/// <summary>
/// Executor for running standalone executable (.exe) files as scheduled tasks.
/// Handles command parsing, process execution, output capture, and timeout management.
/// </summary>
public class ExeExecutor : ITaskExecutor
{
    private readonly TimeSpan _timeout;

    /// <summary>
    /// Initializes a new instance of the ExeExecutor class.
    /// </summary>
    /// <param name="timeout">The maximum duration for process execution. Defaults to 30 minutes if not specified.</param>
    public ExeExecutor(TimeSpan? timeout = null)
    {
        _timeout = timeout ?? TimeSpan.FromMinutes(30);
    }

    /// <summary>
    /// Executes an executable file with the command specified in the task definition.
    /// </summary>
    /// <param name="task">The task definition containing the executable path and arguments.</param>
    /// <returns>The execution result containing output, error, and exit code.</returns>
    /// <exception cref="ArgumentNullException">Thrown when task is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the executable file name is invalid.</exception>
    public async Task<ExecutionResult> ExecuteAsync(TaskDefinition task, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(task);
        using var timeoutCts = new CancellationTokenSource(_timeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        var (fileName, arguments) = CommandParser.Parse(task.Command);

        if (string.IsNullOrWhiteSpace(fileName))
            throw new InvalidOperationException("Invalid executable");

        if (fileName.Equals("cmd.exe", StringComparison.OrdinalIgnoreCase) &&
            !arguments.StartsWith("/c", StringComparison.OrdinalIgnoreCase))
        {
            arguments = "/c " + arguments;
        }

        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
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
                Error = BuildStartError(fileName, ex),
                ExitCode = -1
            };
        }
        catch (UnauthorizedAccessException ex)
        {
            return new ExecutionResult
            {
                Output = string.Empty,
                Error = $"Access denied to '{fileName}': {ex.Message}",
                ExitCode = -1
            };
        }

        try
        {
            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();

            await Task.WhenAll(
                outputTask,
                errorTask,
                process.WaitForExitAsync(linkedCts.Token)
            ).ConfigureAwait(false);

            return new ExecutionResult
            {
                Output = outputTask.Result,
                Error = errorTask.Result,
                ExitCode = process.ExitCode
            };
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(entireProcessTree: true); } catch { /* already finished */ }

            return new ExecutionResult
            {
                Output = string.Empty,
                Error = $"Proceso cancelado: superó el timeout de {_timeout.TotalMinutes} minutos.",
                ExitCode = -1
            };
        }
    }

    private static string BuildStartError(string fileName, System.ComponentModel.Win32Exception ex) =>
        ex.NativeErrorCode switch
        {
            2 => $"File not found: '{fileName}'. Verify the path is correct.",
            3 => $"Directory not found in path: '{fileName}'.",
            5 => $"Access denied: '{fileName}'. Check file permissions.",
            _ => $"Failed to start '{fileName}': {ex.Message} (OS error {ex.NativeErrorCode})."
        };
}
