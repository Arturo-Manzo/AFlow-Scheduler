using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Reflection;
using CHRONIQ.Api.Dtos;
using CHRONIQ.Data;
using CHRONIQ.Queue;
using CHRONIQ.Services;

namespace CHRONIQ.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class StatusController : ControllerBase
{
    private readonly IWorkerStateService _workerState;
    private readonly ITaskQueue _taskQueue;
    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _env;
    private readonly INotificationSettingsRepository _notificationSettingsRepository;
    private readonly IExecutionRepository _executionRepository;
    private readonly IStaleThresholdProvider _staleThresholdProvider;

    public StatusController(
        IWorkerStateService workerState,
        ITaskQueue taskQueue,
        IConfiguration configuration,
        IWebHostEnvironment env,
        INotificationSettingsRepository notificationSettingsRepository,
        IExecutionRepository executionRepository,
        IStaleThresholdProvider staleThresholdProvider)
    {
        ArgumentNullException.ThrowIfNull(workerState);
        ArgumentNullException.ThrowIfNull(taskQueue);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(env);
        ArgumentNullException.ThrowIfNull(notificationSettingsRepository);
        ArgumentNullException.ThrowIfNull(executionRepository);
        ArgumentNullException.ThrowIfNull(staleThresholdProvider);
        _workerState = workerState;
        _taskQueue = taskQueue;
        _configuration = configuration;
        _env = env;
        _notificationSettingsRepository = notificationSettingsRepository;
        _executionRepository = executionRepository;
        _staleThresholdProvider = staleThresholdProvider;
    }

    [HttpGet]
    [Authorize(Roles = "Admin,Operator,Viewer")]
    public async Task<IActionResult> GetStatus()
    {
        var dbConnected = await CheckDatabaseAsync();
        var smtpSettings = await _notificationSettingsRepository.GetEffectiveSmtpSettingsAsync();
        var staleThresholdMinutes = Math.Max(1, _configuration.GetValue<int>("WorkerPool:StaleExecutionThresholdMinutes", 15));
        var staleBeforeUtc = DateTime.UtcNow.AddMinutes(-staleThresholdMinutes);
        var runningExecutions = await _executionRepository.GetRunningExecutionsAsync(staleBeforeUtc);

        // Re-compute IsStale per-task using dynamic thresholds
        foreach (var record in runningExecutions)
        {
            record.IsStale = await _staleThresholdProvider.IsStaleAsync(record.TaskId, record.StartedAt);
        }

        var staleExecutions = runningExecutions.Count(record => record.IsStale);
        var autoRecoveryEnabled = _configuration.GetValue<bool>("WorkerPool:AutoRecoverStaleExecutions", true);

        var dto = new SystemStatusDto
        {
            ApiOnline = true,
            DbConnected = dbConnected,
            ActiveWorkers = _workerState.ActiveWorkerCount,
            TotalWorkers = _workerState.TotalWorkerCount,
            RunningBoxRuns = _workerState.RunningBoxRunCount,
            RunningExecutions = runningExecutions.Count,
            StaleExecutions = staleExecutions,
            StaleExecutionThresholdMinutes = staleThresholdMinutes,
            QueueDepth = _taskQueue.QueueDepth,
            FailNotificationEnabled = smtpSettings.Enabled,
            BackendVersion = GetBackendVersion(),
            AutoRecoveryEnabled = autoRecoveryEnabled,
            StartupRecoveryCompleted = _workerState.StartupRecoveryCompleted,
            LastRecoveryCompletedAtUtc = _workerState.LastRecoveryCompletedAtUtc,
            LastRecoveredExecutionCount = _workerState.LastRecoveredExecutionCount,
            LastRecoveredBoxRunCount = _workerState.LastRecoveredBoxRunCount,
            Environment = _env.EnvironmentName
        };

        return Ok(new ApiResponse<SystemStatusDto> { Success = true, Data = dto });
    }

    private static string GetBackendVersion()
    {
        var entryAssembly = Assembly.GetEntryAssembly();
        if (entryAssembly == null)
            return "unknown";

        var informational = entryAssembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;

        if (!string.IsNullOrWhiteSpace(informational))
        {
            var plusIndex = informational.IndexOf('+');
            return plusIndex > 0 ? informational[..plusIndex] : informational;
        }

        return entryAssembly.GetName().Version?.ToString() ?? "unknown";
    }

    private async Task<bool> CheckDatabaseAsync()
    {
        try
        {
            var connectionString = _configuration.GetConnectionString("Default");
            if (string.IsNullOrEmpty(connectionString)) return false;

            await using var conn = new SqlConnection(connectionString);
            await conn.OpenAsync();
            var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT 1";
            cmd.CommandTimeout = 3;
            await cmd.ExecuteScalarAsync();
            return true;
        }
        catch
        {
            return false;
        }
    }
}
