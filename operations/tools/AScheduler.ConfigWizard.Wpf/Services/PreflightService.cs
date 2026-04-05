using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using AScheduler.ConfigWizard.Wpf.Models;

namespace AScheduler.ConfigWizard.Wpf.Services;

/// <summary>
/// Runs environment checks before writing production configuration.
/// </summary>
public sealed class PreflightService
{
    public async Task<PreflightResult> RunAsync(
        string sqlServer,
        int frontendPort,
        int backendPort,
        bool validateFrontend,
        bool validateBackend,
        bool validateDatabase)
    {
        var dotnetChecked = validateBackend;
        var sqlChecked = validateDatabase;
        var frontendPortChecked = validateFrontend;
        var backendPortChecked = validateBackend;

        var dotnetStatus = "No seleccionado";
        var dotnetAvailable = true;
        if (dotnetChecked)
        {
            var dotnetVersion = await TryGetDotnetVersionAsync().ConfigureAwait(false);
            dotnetStatus = string.IsNullOrWhiteSpace(dotnetVersion)
                ? "No encontrado (requiere .NET 8+)."
                : dotnetVersion.StartsWith("8.", StringComparison.Ordinal)
                    ? $"OK ({dotnetVersion})"
                    : $"Advertencia: detectado {dotnetVersion}, recomendado .NET 8.";
            dotnetAvailable = !string.IsNullOrWhiteSpace(dotnetVersion);
        }

        var sqlStatus = "No seleccionado";
        var sqlOk = true;
        if (sqlChecked)
        {
            sqlStatus = await TrySqlConnectionAsync(sqlServer).ConfigureAwait(false);
            sqlOk = sqlStatus.StartsWith("OK", StringComparison.OrdinalIgnoreCase);
        }

        var frontendPortStatus = "No seleccionado";
        var frontendPortOk = true;
        if (frontendPortChecked)
        {
            frontendPortOk = TestPortAvailability(frontendPort);
            frontendPortStatus = frontendPortOk ? "Disponible" : "Ocupado";
        }

        var backendPortStatus = "No seleccionado";
        var backendPortOk = true;
        if (backendPortChecked)
        {
            backendPortOk = TestPortAvailability(backendPort);
            backendPortStatus = backendPortOk ? "Disponible" : "Ocupado";
        }

        var canContinue = dotnetAvailable && sqlOk && frontendPortOk && backendPortOk;

        return new PreflightResult
        {
            DotnetStatus = dotnetStatus,
            SqlStatus = sqlStatus,
            FrontendPortStatus = frontendPortStatus,
            BackendPortStatus = backendPortStatus,
            DotnetChecked = dotnetChecked,
            SqlChecked = sqlChecked,
            FrontendPortChecked = frontendPortChecked,
            BackendPortChecked = backendPortChecked,
            CanContinue = canContinue
        };
    }

    private static bool TestPortAvailability(int port)
    {
        TcpListener? anyInterfaceListener = null;
        TcpListener? loopbackListener = null;
        try
        {
            // Binding on Any detects conflicts for listeners bound to any interface.
            anyInterfaceListener = new TcpListener(IPAddress.Any, port);
            anyInterfaceListener.Start();

            // Binding on Loopback catches listeners that only use localhost.
            loopbackListener = new TcpListener(IPAddress.Loopback, port);
            loopbackListener.Start();
            return true;
        }
        catch
        {
            return false;
        }
        finally
        {
            loopbackListener?.Stop();
            anyInterfaceListener?.Stop();
        }
    }

    private static async Task<string?> TryGetDotnetVersionAsync()
    {
        var startInfo = new ProcessStartInfo("dotnet", "--version")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        try
        {
            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return null;
            }

            var output = await process.StandardOutput.ReadToEndAsync().ConfigureAwait(false);
            await process.WaitForExitAsync().ConfigureAwait(false);
            return process.ExitCode == 0 ? output.Trim() : null;
        }
        catch
        {
            return null;
        }
    }

    private static async Task<string> TrySqlConnectionAsync(string server)
    {
        var trimmedServer = server?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(trimmedServer))
        {
            return "Servidor SQL vacio.";
        }

        // Supports common SQL Server host formats and LocalDB instance names.
        if (!Regex.IsMatch(trimmedServer, "^[a-zA-Z0-9._\\\\()\\-,:]+$"))
        {
            return "Servidor SQL invalido: contiene caracteres no permitidos.";
        }

        var startInfo = new ProcessStartInfo("sqlcmd")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        startInfo.ArgumentList.Add("-S");
        startInfo.ArgumentList.Add(trimmedServer);
        startInfo.ArgumentList.Add("-d");
        startInfo.ArgumentList.Add("master");
        startInfo.ArgumentList.Add("-Q");
        startInfo.ArgumentList.Add("SELECT 1");
        startInfo.ArgumentList.Add("-b");

        try
        {
            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return "No se pudo lanzar sqlcmd.";
            }

            var error = await process.StandardError.ReadToEndAsync().ConfigureAwait(false);
            await process.WaitForExitAsync().ConfigureAwait(false);

            if (process.ExitCode == 0)
            {
                return $"OK (conexion a {trimmedServer})";
            }

            return string.IsNullOrWhiteSpace(error)
                ? $"No se pudo conectar a {trimmedServer}."
                : $"No se pudo conectar a {trimmedServer}: {error.Trim()}";
        }
        catch
        {
            return "sqlcmd no esta disponible en PATH.";
        }
    }
}
