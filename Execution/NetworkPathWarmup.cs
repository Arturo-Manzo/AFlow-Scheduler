namespace CHRONIQ.Execution;

/// <summary>
/// Touches local or network-backed file locations before launching a scheduled process.
/// This helps Windows services recover stale SMB sessions before Process.Start resolves the path.
/// </summary>
public static class NetworkPathWarmup
{
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(750);
    private const int MaxAttempts = 2;

    /// <summary>
    /// Attempts to access the filesystem location referenced by a command target before process launch.
    /// </summary>
    /// <param name="commandTarget">Executable, script path, or command string whose first token may be a path.</param>
    /// <param name="cancellationToken">Cancellation token for the warmup attempt.</param>
    /// <returns>Null when no warmup is needed or access succeeds; otherwise an execution failure result.</returns>
    public static async Task<ExecutionResult?> WarmCommandTargetAsync(string commandTarget, CancellationToken cancellationToken)
    {
        var path = ExtractPathCandidate(commandTarget);
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathRooted(path))
        {
            return null;
        }

        var target = ResolveWarmupTarget(path);
        if (string.IsNullOrWhiteSpace(target))
        {
            return null;
        }

        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var result = await ProbeAsync(target, cancellationToken).ConfigureAwait(false);
            if (result.IsAccessible)
            {
                return null;
            }

            if (attempt < MaxAttempts)
            {
                await Task.Delay(RetryDelay, cancellationToken).ConfigureAwait(false);
                continue;
            }

            return new ExecutionResult
            {
                Output = string.Empty,
                Error = BuildAccessError(path, target, result.Error),
                ExitCode = -1
            };
        }

        return null;
    }

    /// <summary>
    /// Extracts the first executable/script token from a quoted or unquoted command string.
    /// </summary>
    /// <param name="commandTarget">Command text to inspect.</param>
    /// <returns>The first path-like token, or null when the command is empty or malformed.</returns>
    public static string? ExtractPathCandidate(string commandTarget)
    {
        if (string.IsNullOrWhiteSpace(commandTarget))
        {
            return null;
        }

        var trimmed = commandTarget.Trim();
        if (trimmed.StartsWith("\"", StringComparison.Ordinal))
        {
            var endQuote = trimmed.IndexOf('"', 1);
            return endQuote > 1 ? trimmed[1..endQuote] : null;
        }

        var firstSpace = trimmed.IndexOf(' ');
        return firstSpace < 0 ? trimmed : trimmed[..firstSpace];
    }

    private static string? ResolveWarmupTarget(string path)
    {
        if (LooksLikeDirectory(path))
        {
            return path;
        }

        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            return directory;
        }

        return Path.GetPathRoot(path);
    }

    private static bool LooksLikeDirectory(string path)
    {
        if (path.EndsWith(Path.DirectorySeparatorChar) || path.EndsWith(Path.AltDirectorySeparatorChar))
        {
            return true;
        }

        return string.IsNullOrWhiteSpace(Path.GetExtension(path));
    }

    private static async Task<ProbeResult> ProbeAsync(string target, CancellationToken cancellationToken)
    {
        Exception? probeException = null;
        var probeTask = Task.Run(() =>
        {
            try
            {
                return Directory.Exists(target) || File.Exists(target);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
            {
                probeException = ex;
                return false;
            }
        }, cancellationToken);

        var completedTask = await Task.WhenAny(
            probeTask,
            Task.Delay(ProbeTimeout, cancellationToken)).ConfigureAwait(false);

        if (completedTask != probeTask)
        {
            return new ProbeResult(false, $"Timed out after {ProbeTimeout.TotalSeconds:0} seconds.");
        }

        return await probeTask.ConfigureAwait(false)
            ? new ProbeResult(true, null)
            : new ProbeResult(false, probeException?.Message);
    }

    private static string BuildAccessError(string path, string target, string? detail)
    {
        var message = $"Unable to access path before process start: '{path}'. Warmup target: '{target}'.";
        if (!string.IsNullOrWhiteSpace(detail))
        {
            message += $" Detail: {detail}";
        }

        if (IsMappedDrivePath(path))
        {
            message += " If this is a mapped drive, prefer a UNC path (\\\\server\\share\\...) and make sure the Windows service account has permissions.";
        }

        return message;
    }

    private static bool IsMappedDrivePath(string path) =>
        path.Length >= 3 &&
        char.IsLetter(path[0]) &&
        path[1] == ':' &&
        (path[2] == Path.DirectorySeparatorChar || path[2] == Path.AltDirectorySeparatorChar);

    private sealed record ProbeResult(bool IsAccessible, string? Error);
}
