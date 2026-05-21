using System.Diagnostics;
using System.Reflection;
using CHRONIQ.Api.Dtos;
using CHRONIQ.Data;
using CHRONIQ.Domain;
using CHRONIQ.Queue;
using CHRONIQ.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace CHRONIQ.Api.Controllers;

[ApiController]
[Route("api/health-dashboard")]
[Authorize(Roles = "Admin")]
public sealed class HealthDashboardController : ControllerBase
{
    private static readonly string[] FailureStatuses = ["Failed", "Aborted", "NotExecuted", "Skipped", "Partial"];

    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _environment;
    private readonly IWorkerStateService _workerState;
    private readonly ITaskQueue _taskQueue;
    private readonly IExecutionRepository _executionRepository;
    private readonly IApplicationLogRepository _applicationLogRepository;
    private readonly INotificationSettingsRepository _notificationSettingsRepository;
    private readonly IStaleThresholdProvider _staleThresholdProvider;
    private readonly ILogger<HealthDashboardController> _logger;

    public HealthDashboardController(
        IConfiguration configuration,
        IWebHostEnvironment environment,
        IWorkerStateService workerState,
        ITaskQueue taskQueue,
        IExecutionRepository executionRepository,
        IApplicationLogRepository applicationLogRepository,
        INotificationSettingsRepository notificationSettingsRepository,
        IStaleThresholdProvider staleThresholdProvider,
        ILogger<HealthDashboardController> logger)
    {
        _configuration = configuration;
        _environment = environment;
        _workerState = workerState;
        _taskQueue = taskQueue;
        _executionRepository = executionRepository;
        _applicationLogRepository = applicationLogRepository;
        _notificationSettingsRepository = notificationSettingsRepository;
        _staleThresholdProvider = staleThresholdProvider;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] int hours = 24, [FromQuery] int limit = 50)
    {
        var safeHours = Math.Clamp(hours <= 0 ? 24 : hours, 1, 168);
        var safeLimit = Math.Clamp(limit <= 0 ? 50 : limit, 1, 200);
        var fromUtc = DateTime.UtcNow.AddHours(-safeHours);

        var databaseCheck = await CheckDatabaseAsync();
        var status = await BuildStatusAsync(databaseCheck.IsHealthy);
        var readiness = await BuildReadinessAsync(databaseCheck);
        var appErrors = await TryLoadApplicationLogsAsync(fromUtc, safeLimit);
        var appSummary = await TryLoadApplicationLogSummaryAsync(fromUtc);
        var failedExecutions = await TryLoadFailedExecutionsAsync(fromUtc, safeLimit);
        var failedExecutionCount = await TryLoadFailedExecutionCountAsync(fromUtc, failedExecutions.Count);

        var dto = new HealthDashboardDto
        {
            Status = status,
            Readiness = readiness,
            ApplicationErrors = appErrors,
            FailedExecutions = failedExecutions,
            WindowHours = safeHours,
            Limit = safeLimit,
            GeneratedAtUtc = DateTime.UtcNow,
            Summary = new HealthSummaryDto
            {
                AppErrors = appSummary.ErrorCount,
                AppWarnings = appSummary.WarningCount,
                FailedExecutions = failedExecutionCount,
                StaleExecutions = status.StaleExecutions,
                QueueDepth = status.QueueDepth,
                DbConnected = status.DbConnected
            }
        };

        return Ok(new ApiResponse<HealthDashboardDto> { Success = true, Data = dto });
    }

    private async Task<SystemStatusDto> BuildStatusAsync(bool dbConnected)
    {
        var staleThresholdMinutes = Math.Max(1, _configuration.GetValue<int>("WorkerPool:StaleExecutionThresholdMinutes", 15));
        var runningExecutions = new List<ExecutionRepository.ExecutionRecord>();

        if (dbConnected)
        {
            try
            {
                runningExecutions = await _executionRepository.GetRunningExecutionsAsync(DateTime.UtcNow.AddMinutes(-staleThresholdMinutes));
                foreach (var record in runningExecutions)
                {
                    record.IsStale = await _staleThresholdProvider.IsStaleAsync(record.TaskId, record.StartedAt);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Health dashboard could not load running executions.");
            }
        }

        return new SystemStatusDto
        {
            ApiOnline = true,
            DbConnected = dbConnected,
            ActiveWorkers = _workerState.ActiveWorkerCount,
            TotalWorkers = _workerState.TotalWorkerCount,
            RunningBoxRuns = _workerState.RunningBoxRunCount,
            RunningExecutions = runningExecutions.Count,
            StaleExecutions = runningExecutions.Count(record => record.IsStale),
            StaleExecutionThresholdMinutes = staleThresholdMinutes,
            QueueDepth = _taskQueue.QueueDepth,
            FailNotificationEnabled = await TryGetFailureNotificationsEnabledAsync(),
            BackendVersion = GetBackendVersion(),
            AutoRecoveryEnabled = _configuration.GetValue<bool>("WorkerPool:AutoRecoverStaleExecutions", true),
            StartupRecoveryCompleted = _workerState.StartupRecoveryCompleted,
            LastRecoveryCompletedAtUtc = _workerState.LastRecoveryCompletedAtUtc,
            LastRecoveredExecutionCount = _workerState.LastRecoveredExecutionCount,
            LastRecoveredBoxRunCount = _workerState.LastRecoveredBoxRunCount,
            Environment = _environment.EnvironmentName
        };
    }

    private async Task<List<ReadinessCheckDto>> BuildReadinessAsync(DatabaseProbe databaseCheck)
    {
        var checks = new List<ReadinessCheckDto>
        {
            new()
            {
                Name = "self",
                Status = "Healthy",
                Description = "API process is alive.",
                DurationMs = 0
            },
            new()
            {
                Name = "database",
                Status = databaseCheck.IsHealthy ? "Healthy" : "Unhealthy",
                Description = databaseCheck.Description,
                DurationMs = databaseCheck.DurationMs
            }
        };

        var workerStopwatch = Stopwatch.StartNew();
        var totalWorkers = _workerState.TotalWorkerCount;
        var queueDepth = _taskQueue.QueueDepth;
        var queueDepthDegradedThreshold = Math.Max(1, _configuration.GetValue<int>("WorkerPool:QueueDepthDegradedThreshold", 50));
        var staleCount = 0;
        var workerStatus = totalWorkers <= 0 ? "Unhealthy" : "Healthy";
        var workerDescription = totalWorkers <= 0
            ? "Worker pool has no configured workers."
            : "Worker pool is ready.";

        if (totalWorkers > 0 && databaseCheck.IsHealthy)
        {
            try
            {
                var staleThresholdMinutes = Math.Max(1, _configuration.GetValue<int>("WorkerPool:StaleExecutionThresholdMinutes", 15));
                var runningExecutions = await _executionRepository.GetRunningExecutionsAsync(DateTime.UtcNow.AddMinutes(-staleThresholdMinutes));
                foreach (var record in runningExecutions)
                {
                    record.IsStale = await _staleThresholdProvider.IsStaleAsync(record.TaskId, record.StartedAt);
                }

                staleCount = runningExecutions.Count(record => record.IsStale);
            }
            catch (Exception ex)
            {
                workerStatus = "Degraded";
                workerDescription = "Worker state is available, but execution freshness could not be loaded.";
                _logger.LogWarning(ex, "Health dashboard worker readiness could not load execution freshness.");
            }
        }
        else if (totalWorkers > 0 && !databaseCheck.IsHealthy)
        {
            workerStatus = "Degraded";
            workerDescription = "Worker state is available, but database-dependent readiness is unavailable.";
        }

        if (workerStatus == "Healthy" && (staleCount > 0 || queueDepth >= queueDepthDegradedThreshold))
        {
            workerStatus = "Degraded";
            workerDescription = "Worker pool is under pressure (stale executions or high queue depth).";
        }

        workerStopwatch.Stop();
        checks.Add(new ReadinessCheckDto
        {
            Name = "workerpool",
            Status = workerStatus,
            Description = workerDescription,
            DurationMs = workerStopwatch.Elapsed.TotalMilliseconds
        });

        return checks;
    }

    private async Task<DatabaseProbe> CheckDatabaseAsync()
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var connectionString = _configuration.GetConnectionString("Default");
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                stopwatch.Stop();
                return new DatabaseProbe(false, "Database connection string is not configured.", stopwatch.Elapsed.TotalMilliseconds);
            }

            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();
            var command = connection.CreateCommand();
            command.CommandText = "SELECT 1";
            command.CommandTimeout = 3;
            await command.ExecuteScalarAsync();
            stopwatch.Stop();
            return new DatabaseProbe(true, "Database reachable.", stopwatch.Elapsed.TotalMilliseconds);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return new DatabaseProbe(false, $"Database connectivity failed: {ex.Message}", stopwatch.Elapsed.TotalMilliseconds);
        }
    }

    private async Task<List<ApplicationLogDto>> TryLoadApplicationLogsAsync(DateTime fromUtc, int limit)
    {
        try
        {
            var records = await _applicationLogRepository.GetRecentAsync(fromUtc, limit);
            return records.Select(MapToDto).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Health dashboard could not load application logs.");
            return new List<ApplicationLogDto>();
        }
    }

    private async Task<ApplicationLogSummary> TryLoadApplicationLogSummaryAsync(DateTime fromUtc)
    {
        try
        {
            return await _applicationLogRepository.GetSummaryAsync(fromUtc);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Health dashboard could not load application log summary.");
            return new ApplicationLogSummary();
        }
    }

    private async Task<List<ExecutionDto>> TryLoadFailedExecutionsAsync(DateTime fromUtc, int limit)
    {
        try
        {
            var records = await _executionRepository.GetFailedExecutionsAsync(
                limit,
                fromUtc: fromUtc,
                status: FailureStatuses);
            return records.Select(MapToDto).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Health dashboard could not load failed executions.");
            return new List<ExecutionDto>();
        }
    }

    private async Task<int> TryLoadFailedExecutionCountAsync(DateTime fromUtc, int fallback)
    {
        try
        {
            return await _executionRepository.GetFailedExecutionCountAsync(fromUtc, status: FailureStatuses);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Health dashboard could not load failed execution count.");
            return fallback;
        }
    }

    private async Task<bool> TryGetFailureNotificationsEnabledAsync()
    {
        try
        {
            var settings = await _notificationSettingsRepository.GetEffectiveSmtpSettingsAsync();
            return settings.Enabled;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Health dashboard could not load SMTP notification settings.");
            return false;
        }
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

    private static ApplicationLogDto MapToDto(ApplicationLogRecord record) => new()
    {
        Id = record.Id,
        LogFileName = record.LogFileName,
        Timestamp = record.Timestamp,
        Level = record.Level,
        Message = record.Message,
        ErrorFile = record.ErrorFile,
        ErrorMethod = record.ErrorMethod,
        ErrorLine = record.ErrorLine,
        ExceptionType = record.ExceptionType,
        Source = record.Source,
        CorrelationId = record.CorrelationId,
        UserId = record.UserId,
        RequestPath = record.RequestPath,
        StatusCode = record.StatusCode,
        CreatedAt = record.CreatedAt
    };

    private static ExecutionDto MapToDto(ExecutionRepository.ExecutionRecord r) => new()
    {
        ExecutionId = r.ExecutionId,
        TaskId = r.TaskId,
        TaskName = r.TaskName,
        TaskType = r.TaskType,
        Command = r.Command,
        BoxId = r.BoxId,
        BoxName = r.BoxName,
        BoxTimeZoneId = r.BoxTimeZoneId,
        DepartmentName = r.DepartmentName,
        FailureAlertEmail = r.FailureAlertEmail,
        BoxRunId = r.BoxRunId,
        StartedAt = r.StartedAt,
        EndedAt = r.EndedAt,
        Status = r.Status,
        ExitCode = r.ExitCode,
        StdOut = r.StdOut,
        StdErr = r.StdErr,
        DurationSeconds = r.EndedAt.HasValue ? (int?)(r.EndedAt.Value - r.StartedAt).TotalSeconds : null,
        TriggerSource = r.TriggerSource,
        Reason = r.Reason,
        RequestedByUserId = r.RequestedByUserId,
        RequestedByUsername = r.RequestedByUsername,
        ErrorMessage = string.IsNullOrWhiteSpace(r.Error) ? null : r.Error,
        IsStale = r.IsStale
    };

    private sealed record DatabaseProbe(bool IsHealthy, string Description, double DurationMs);
}
