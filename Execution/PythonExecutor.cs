using System.Diagnostics;
using AScheduler.Domain;

namespace AScheduler.Execution;

/// <summary>
/// Executor for running Python scripts.
/// Command should be script path + args, e.g. "C:\scripts\myjob.py --flag value".
/// </summary>
public class PythonExecutor : ITaskExecutor
{
    private readonly TimeSpan _timeout;
    private readonly string _pythonExecutable;

    public PythonExecutor(TimeSpan? timeout = null, string pythonExecutable = "python")
    {
        _timeout = timeout ?? TimeSpan.FromMinutes(30);
        _pythonExecutable = string.IsNullOrWhiteSpace(pythonExecutable) ? "python" : pythonExecutable;
    }

    public async Task<ExecutionResult> ExecuteAsync(TaskDefinition task, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(task);
        if (string.IsNullOrWhiteSpace(task.Command))
            throw new ArgumentException("Python task command cannot be empty.");

        var (scriptPath, args) = CommandParser.Parse(task.Command);
        if (string.IsNullOrWhiteSpace(scriptPath))
            throw new ArgumentException("Python task command does not include a valid script path.");

        using var timeoutCts = new CancellationTokenSource(_timeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = _pythonExecutable,
                Arguments = $"\"{scriptPath}\" {args}".Trim(),
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
                Error = ex.NativeErrorCode == 2
                    ? $"Python interpreter not found: '{_pythonExecutable}'. Ensure Python is installed and in PATH."
                    : $"Failed to start Python: {ex.Message} (OS error {ex.NativeErrorCode}).",
                ExitCode = -1
            };
        }
        catch (UnauthorizedAccessException ex)
        {
            return new ExecutionResult
            {
                Output = string.Empty,
                Error = $"Access denied to '{scriptPath}': {ex.Message}",
                ExitCode = -1
            };
        }

        try
        {
            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();

            await Task.WhenAll(outputTask, errorTask, process.WaitForExitAsync(linkedCts.Token));

            return new ExecutionResult
            {
                Output = outputTask.Result,
                Error = errorTask.Result,
                ExitCode = process.ExitCode
            };
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(entireProcessTree: true); } catch { }

            return new ExecutionResult
            {
                Output = string.Empty,
                Error = $"Python execution timed out after {_timeout.TotalSeconds} seconds.",
                ExitCode = -1
            };
        }
    }
}
