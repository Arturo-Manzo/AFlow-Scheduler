using System.Security.Claims;
using Cronos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using AScheduler.Api.Dtos;
using AScheduler.Api.Services;
using AScheduler.Data;
using AScheduler.Domain;
using AScheduler.Queue;
using AScheduler.Services;
using TimeZoneConverter;

namespace AScheduler.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class BoxesController : ControllerBase
{
    private static readonly HashSet<string> ValidIanaTimeZones = new(TZConvert.KnownIanaTimeZoneNames, StringComparer.Ordinal);

    private readonly IBoxRepository _boxRepository;
    private readonly ITaskRepository _taskRepository;
    private readonly IAuditLogService _auditLog;
    private readonly IWorkerStateService _workerState;
    private readonly ILogger<BoxesController> _logger;

    public BoxesController(
        IBoxRepository boxRepository,
        ITaskRepository taskRepository,
        IAuditLogService auditLog,
        IWorkerStateService workerState,
        ILogger<BoxesController> logger)
    {
        ArgumentNullException.ThrowIfNull(boxRepository);
        ArgumentNullException.ThrowIfNull(taskRepository);
        ArgumentNullException.ThrowIfNull(auditLog);
        ArgumentNullException.ThrowIfNull(workerState);
        ArgumentNullException.ThrowIfNull(logger);
        _boxRepository = boxRepository;
        _taskRepository = taskRepository;
        _auditLog = auditLog;
        _workerState = workerState;
        _logger = logger;
    }

    [HttpGet]
    [Authorize(Roles = "Admin,Operator,Viewer")]
    public async Task<IActionResult> GetAll()
    {
        var boxes = await _boxRepository.GetActiveBoxesAsync();
        var dtos = new List<BoxDto>();
        foreach (var box in boxes)
            dtos.Add(await MapToBoxDtoAsync(box, includeTasks: true));
        return Ok(new ApiResponse<List<BoxDto>> { Success = true, Data = dtos });
    }

    [HttpGet("{boxId}")]
    [Authorize(Roles = "Admin,Operator,Viewer")]
    public async Task<IActionResult> GetById(int boxId)
    {
        var box = await _boxRepository.GetByIdAsync(boxId);
        if (box == null)
            return NotFound(new ApiResponse<object> { Success = false, Message = "Box not found.", ErrorCode = "BOX_NOT_FOUND" });
        var dto = await MapToBoxDtoAsync(box, includeTasks: true);
        return Ok(new ApiResponse<BoxDto> { Success = true, Data = dto });
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create([FromBody] CreateBoxRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(new ApiResponse<object> { Success = false, Message = "Name is required.", ErrorCode = "MISSING_FIELDS" });

        if (!TryValidateSchedule(request.CronExpression, request.TimeZoneId, out var scheduleError))
            return BadRequest(new ApiResponse<object> { Success = false, Message = scheduleError, ErrorCode = "INVALID_SCHEDULE" });

        if (request.InitialTask == null ||
            string.IsNullOrWhiteSpace(request.InitialTask.Name) ||
            string.IsNullOrWhiteSpace(request.InitialTask.Command) ||
            string.IsNullOrWhiteSpace(request.InitialTask.TaskType))
            return BadRequest(new ApiResponse<object> { Success = false, Message = "A box requires at least one task. Provide initialTask with name, command, and taskType.", ErrorCode = "MISSING_INITIAL_TASK" });

        try
        {
            var boxId = await _boxRepository.CreateAsync(
                request.Name, request.Description, request.CronExpression, request.TimeZoneId, false);

            await _taskRepository.CreateAsync(
                boxId,
                request.InitialTask.Name,
                request.InitialTask.Description,
                request.InitialTask.Command,
                request.InitialTask.TaskType);

            var userId = GetCurrentUserId();
            if (userId.HasValue)
                await _auditLog.LogAsync(userId.Value, "Boxes", boxId, "Create", newValues: request.Name);

            var box = await _boxRepository.GetByIdAsync(boxId);
            var dto = await MapToBoxDtoAsync(box!, includeTasks: true);
            return CreatedAtAction(nameof(GetById), new { boxId }, new ApiResponse<BoxDto> { Success = true, Data = dto });
        }
        catch (SqlException ex) when (ex.Number is 2601 or 2627)
        {
            _logger.LogWarning(ex, "Duplicate box name: {BoxName}", request.Name);
            return BadRequest(new ApiResponse<object>
            {
                Success = false,
                Message = "A box with this name already exists.",
                ErrorCode = "BOX_NAME_EXISTS"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating box.");
            return BadRequest(new ApiResponse<object> { Success = false, Message = "Error creating box.", ErrorCode = "CREATION_ERROR" });
        }
    }

    [HttpPut("{boxId}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Update(int boxId, [FromBody] UpdateBoxRequest request)
    {
        var existing = await _boxRepository.GetByIdAsync(boxId);
        if (existing == null)
            return NotFound(new ApiResponse<object> { Success = false, Message = "Box not found.", ErrorCode = "BOX_NOT_FOUND" });

        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(new ApiResponse<object> { Success = false, Message = "Name is required.", ErrorCode = "MISSING_FIELDS" });

        if (!TryValidateSchedule(request.CronExpression, request.TimeZoneId, out var scheduleError))
            return BadRequest(new ApiResponse<object> { Success = false, Message = scheduleError, ErrorCode = "INVALID_SCHEDULE" });

        await _boxRepository.UpdateAsync(boxId, request.Name, request.Description, request.CronExpression, request.TimeZoneId, false, request.Enabled);

        var userId = GetCurrentUserId();
        if (userId.HasValue)
            await _auditLog.LogAsync(userId.Value, "Boxes", boxId, "Update", newValues: request.Name);

        var box = await _boxRepository.GetByIdAsync(boxId);
        var dto = await MapToBoxDtoAsync(box!, includeTasks: false);
        return Ok(new ApiResponse<BoxDto> { Success = true, Data = dto });
    }

    [HttpDelete("{boxId}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int boxId)
    {
        var existing = await _boxRepository.GetByIdAsync(boxId);
        if (existing == null)
            return NotFound(new ApiResponse<object> { Success = false, Message = "Box not found.", ErrorCode = "BOX_NOT_FOUND" });

        await _boxRepository.DeleteAsync(boxId);

        var userId = GetCurrentUserId();
        if (userId.HasValue)
            await _auditLog.LogAsync(userId.Value, "Boxes", boxId, "Delete");

        return Ok(new ApiResponse<object> { Success = true, Message = "Box deleted." });
    }

    [HttpPost("{boxId}/run")]
    [Authorize(Roles = "Admin,Operator")]
    public async Task<IActionResult> RunNow(int boxId, [FromBody] ExecuteBoxRequest request)
    {
        var box = await _boxRepository.GetByIdAsync(boxId);
        if (box == null)
            return NotFound(new ApiResponse<object> { Success = false, Message = "Box not found.", ErrorCode = "BOX_NOT_FOUND" });

        if (string.IsNullOrWhiteSpace(request.Reason))
            return BadRequest(new ApiResponse<object> { Success = false, Message = "Reason is required.", ErrorCode = "REASON_REQUIRED" });

        // Reject if any task in this box is already running.
        var tasks = await _taskRepository.GetTasksForBoxAsync(boxId);
        var runningTask = tasks.FirstOrDefault(t => _workerState.IsTaskRunning(t.Id));
        if (runningTask != null)
            return Conflict(new ApiResponse<object>
            {
                Success = false,
                Message = $"Box cannot be started because task '{runningTask.Name}' (ID {runningTask.Id}) is already running.",
                ErrorCode = "TASK_ALREADY_RUNNING"
            });

        var userId = GetCurrentUserId();
        var queueId = await _boxRepository.InsertQueueItemAsync(
            boxId, userId, request.IgnoreDependencies, request.IgnoreSchedule, request.Reason);

        if (userId.HasValue)
            await _auditLog.LogAsync(userId.Value, "Boxes", boxId, "RunNow", newValues: request.Reason);

        return Ok(new ApiResponse<object> { Success = true, Data = new { QueueId = queueId }, Message = "Box queued for execution." });
    }

    private async Task<BoxDto> MapToBoxDtoAsync(BoxDefinition box, bool includeTasks)
    {
        var dto = new BoxDto
        {
            BoxId = box.Id,
            Name = box.Name,
            Description = box.Description,
            CronExpression = box.CronExpression,
            TimeZoneId = box.TimeZoneId,
            Enabled = box.Enabled,
            LastRunUtc = box.LastRunUtc,
            CreatedAt = box.CreatedAtUtc
        };
        if (includeTasks)
        {
            var tasks = await _taskRepository.GetTasksForBoxAsync(box.Id);
            foreach (var t in tasks)
            {
                var deps = await _taskRepository.GetTaskDependenciesAsync(t.Id);
                dto.Tasks.Add(new TaskDto
                {
                    TaskId = t.Id, BoxId = t.BoxId, Name = t.Name, Description = t.Description,
                    Command = t.Command, TaskType = t.TaskType.ToString(),
                    Enabled = t.Enabled, CreatedAt = t.CreatedAtUtc,
                    DependencyTaskIds = deps
                });
            }
        }
        return dto;
    }

    private static bool TryValidateSchedule(string cronExpression, string timeZoneId, out string error)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId))
        {
            error = "Time zone is required.";
            return false;
        }

        if (!ValidIanaTimeZones.Contains(timeZoneId))
        {
            error = "Time zone is invalid. Use a valid IANA time zone ID.";
            return false;
        }

        try
        {
            _ = TZConvert.GetTimeZoneInfo(timeZoneId);
        }
        catch
        {
            error = "Time zone is invalid. Use a valid IANA time zone ID.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(cronExpression))
        {
            error = "Cron expression is required.";
            return false;
        }

        try
        {
            _ = CronExpression.Parse(cronExpression);
        }
        catch
        {
            error = "Cron expression is invalid.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private int? GetCurrentUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier) ?? User.FindFirst("sub") ?? User.FindFirst("userId");
        return claim != null && int.TryParse(claim.Value, out var id) ? id : null;
    }
}
