using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using AScheduler.Api.Dtos;
using AScheduler.Data;
using AScheduler.Queue;
using AScheduler.Services;

namespace AScheduler.Api.Controllers;

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

    public StatusController(
        IWorkerStateService workerState,
        ITaskQueue taskQueue,
        IConfiguration configuration,
        IWebHostEnvironment env,
        INotificationSettingsRepository notificationSettingsRepository)
    {
        ArgumentNullException.ThrowIfNull(workerState);
        ArgumentNullException.ThrowIfNull(taskQueue);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(env);
        ArgumentNullException.ThrowIfNull(notificationSettingsRepository);
        _workerState = workerState;
        _taskQueue = taskQueue;
        _configuration = configuration;
        _env = env;
        _notificationSettingsRepository = notificationSettingsRepository;
    }

    [HttpGet]
    [Authorize(Roles = "Admin,Operator,Viewer")]
    public async Task<IActionResult> GetStatus()
    {
        var dbConnected = await CheckDatabaseAsync();
        var smtpSettings = await _notificationSettingsRepository.GetEffectiveSmtpSettingsAsync();

        var dto = new SystemStatusDto
        {
            ApiOnline = true,
            DbConnected = dbConnected,
            ActiveWorkers = _workerState.ActiveWorkerCount,
            TotalWorkers = _workerState.TotalWorkerCount,
            QueueDepth = _taskQueue.QueueDepth,
            FailNotificationEnabled = smtpSettings.Enabled,
            Environment = _env.EnvironmentName
        };

        return Ok(new ApiResponse<SystemStatusDto> { Success = true, Data = dto });
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
