using System.Security.Claims;
using Cronos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using CHRONIQ.Api.Dtos;
using CHRONIQ.Api.Services;
using CHRONIQ.Data;
using CHRONIQ.Domain;
using CHRONIQ.Queue;
using CHRONIQ.Services;
using TimeZoneConverter;

namespace CHRONIQ.Api.Controllers;

/// <summary>
/// Provides CRUD and operational endpoints for boxes and box runs.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class BoxesController : ControllerBase
{
    private static readonly HashSet<string> ValidIanaTimeZones = new(TZConvert.KnownIanaTimeZoneNames, StringComparer.Ordinal);

    private readonly IBoxRepository _boxRepository;
    private readonly ITaskRepository _taskRepository;
    private readonly ITaskQueue _taskQueue;
    private readonly IAuditLogService _auditLog;
    private readonly IWorkerStateService _workerState;
    private readonly ILogger<BoxesController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="BoxesController"/> class.
    /// </summary>
    /// <param name="boxRepository">Repository for box definitions and runs.</param>
    /// <param name="taskRepository">Repository for tasks and task dependencies.</param>
    /// <param name="taskQueue">Queue used to schedule box execution requests.</param>
    /// <param name="auditLog">Audit logging service.</param>
    /// <param name="workerState">Worker state service for runtime checks.</param>
    /// <param name="logger">Logger instance.</param>
    public BoxesController(
        IBoxRepository boxRepository,
        ITaskRepository taskRepository,
        ITaskQueue taskQueue,
        IAuditLogService auditLog,
        IWorkerStateService workerState,
        ILogger<BoxesController> logger)
    {
        ArgumentNullException.ThrowIfNull(boxRepository);
        ArgumentNullException.ThrowIfNull(taskRepository);
        ArgumentNullException.ThrowIfNull(taskQueue);
        ArgumentNullException.ThrowIfNull(auditLog);
        ArgumentNullException.ThrowIfNull(workerState);
        ArgumentNullException.ThrowIfNull(logger);
        _boxRepository = boxRepository;
        _taskRepository = taskRepository;
        _taskQueue = taskQueue;
        _auditLog = auditLog;
        _workerState = workerState;
        _logger = logger;
    }

    /// <summary>
    /// Gets all active boxes visible to the current user's department scope.
    /// </summary>
    /// <returns>A list of boxes with their tasks.</returns>
    [HttpGet]
    [Authorize(Roles = "Admin,Operator,Viewer")]
    public async Task<IActionResult> GetAll()
    {
        var departmentId = GetCurrentDepartmentId();
        var boxes = await _boxRepository.GetActiveBoxesByDepartmentAsync(departmentId);
        var dtos = new List<BoxDto>();
        foreach (var box in boxes)
            dtos.Add(await MapToBoxDtoAsync(box, includeTasks: true));
        return Ok(new ApiResponse<List<BoxDto>> { Success = true, Data = dtos });
    }

    /// <summary>
    /// Gets a box by identifier.
    /// </summary>
    /// <param name="boxId">Box identifier.</param>
    /// <returns>The requested box when accessible; otherwise an error response.</returns>
    [HttpGet("{boxId}")]
    [Authorize(Roles = "Admin,Operator,Viewer")]
    public async Task<IActionResult> GetById(int boxId)
    {
        var box = await _boxRepository.GetByIdAsync(boxId);
        if (box == null)
            return NotFound(new ApiResponse<object> { Success = false, Message = "Box not found.", ErrorCode = "BOX_NOT_FOUND" });
        
        // Authorization: Check if user can access this box's department
        var userDepartmentId = GetCurrentDepartmentId();
        if (userDepartmentId.HasValue && box.DepartmentId.HasValue && box.DepartmentId != userDepartmentId)
            return Forbid("You do not have permission to access this box.");
        
        var dto = await MapToBoxDtoAsync(box, includeTasks: true);
        return Ok(new ApiResponse<BoxDto> { Success = true, Data = dto });
    }

    /// <summary>
    /// Creates a new box with its initial task.
    /// </summary>
    /// <param name="request">Creation payload.</param>
    /// <returns>The created box response.</returns>
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

        if (!TryValidateEmail(request.NotificationEmail, out var emailError))
            return BadRequest(new ApiResponse<object> { Success = false, Message = emailError, ErrorCode = "INVALID_EMAIL" });

        try
        {
            var boxId = await _boxRepository.CreateAsync(
                request.Name, request.Description, request.CronExpression, request.TimeZoneId, false, request.NotificationEmail, request.DepartmentId);

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

    /// <summary>
    /// Updates an existing box.
    /// </summary>
    /// <param name="boxId">Box identifier.</param>
    /// <param name="request">Update payload.</param>
    /// <returns>The updated box response.</returns>
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

        if (!TryValidateEmail(request.NotificationEmail, out var emailError))
            return BadRequest(new ApiResponse<object> { Success = false, Message = emailError, ErrorCode = "INVALID_EMAIL" });

        await _boxRepository.UpdateAsync(boxId, request.Name, request.Description, request.CronExpression, request.TimeZoneId, false, request.Enabled, request.NotificationEmail, request.DepartmentId);

        var userId = GetCurrentUserId();
        if (userId.HasValue)
            await _auditLog.LogAsync(userId.Value, "Boxes", boxId, "Update", newValues: request.Name);

        var box = await _boxRepository.GetByIdAsync(boxId);
        var dto = await MapToBoxDtoAsync(box!, includeTasks: false);
        return Ok(new ApiResponse<BoxDto> { Success = true, Data = dto });
    }

    /// <summary>
    /// Deletes a box.
    /// </summary>
    /// <param name="boxId">Box identifier.</param>
    /// <returns>A success response when deletion completes.</returns>
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

    /// <summary>
    /// Queues a box for immediate execution.
    /// </summary>
    /// <param name="boxId">Box identifier.</param>
    /// <param name="request">Execution options.</param>
    /// <returns>A queue tracking identifier for the enqueued run.</returns>
    [HttpPost("{boxId}/run")]
    [HttpPost("/api/box/{boxId}/run")]
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

    /// <summary>
    /// Resumes a previously failed, partial, or cancelled box run.
    /// </summary>
    /// <param name="boxRunId">Box run identifier.</param>
    /// <returns>A response indicating whether the run was queued for resume.</returns>
    [HttpPost("runs/{boxRunId}/resume")]
    [HttpPost("/api/box/{boxRunId}/resume")]
    [Authorize(Roles = "Admin,Operator")]
    public async Task<IActionResult> Resume(int boxRunId)
    {
        var boxRun = await _boxRepository.GetBoxRunAsync(boxRunId);
        if (boxRun == null)
            return NotFound(new ApiResponse<object> { Success = false, Message = "BoxRun not found.", ErrorCode = "BOXRUN_NOT_FOUND" });

        // Only Failed, Partial or Cancelled BoxRuns can be resumed manually.
        // Running BoxRuns are auto-resumed on startup.
        if (boxRun.Status is not ("Failed" or "Partial" or "Cancelled"))
            return BadRequest(new ApiResponse<object>
            {
                Success = false,
                Message = $"BoxRun is in '{boxRun.Status}' state. Only Failed, Partial or Cancelled runs can be resumed.",
                ErrorCode = "INVALID_RESUME_STATE"
            });

        await _boxRepository.UpdateBoxRunCancellationAsync(boxRunId, false);

        // Set status back to Running so the worker picks it up correctly.
        await _boxRepository.UpdateBoxRunStatusAsync(boxRunId, "Running", null, null);

        var enqueued = await _taskQueue.EnqueueAsync(new BoxRunRequest
        {
            BoxRunId = boxRun.BoxRunId,
            BoxId = boxRun.BoxId,
            RequestedAt = DateTime.UtcNow,
            TriggerSource = TriggerSources.Retry,
            ScheduledForUtc = boxRun.ScheduledForUtc,
            RequestedByUserId = boxRun.RequestedByUserId,
            IsResume = true
        });

        if (!enqueued)
            return Conflict(new ApiResponse<object>
            {
                Success = false,
                Message = "BoxRun is already pending in the queue.",
                ErrorCode = "ALREADY_QUEUED"
            });

        var userId = GetCurrentUserId();
        if (userId.HasValue)
            await _auditLog.LogAsync(userId.Value, "BoxRuns", boxRunId, "Resume");

        return Ok(new ApiResponse<object> { Success = true, Data = new { BoxRunId = boxRunId }, Message = "BoxRun queued for resume." });
    }

    [HttpPost("runs/{boxRunId}/cancel")]
    [HttpPost("/api/box/{boxRunId}/cancel")]
    [Authorize(Roles = "Admin,Operator")]
    public async Task<IActionResult> Cancel(int boxRunId)
    {
        var boxRun = await _boxRepository.GetBoxRunAsync(boxRunId);
        if (boxRun == null)
            return NotFound(new ApiResponse<object> { Success = false, Message = "BoxRun not found.", ErrorCode = "BOXRUN_NOT_FOUND" });

        if (boxRun.Status is not ("Pending" or "Running"))
            return BadRequest(new ApiResponse<object>
            {
                Success = false,
                Message = $"BoxRun is in '{boxRun.Status}' state. Only Pending or Running runs can be cancelled.",
                ErrorCode = "INVALID_CANCEL_STATE"
            });

        await _boxRepository.UpdateBoxRunCancellationAsync(boxRunId, true);

        var userId = GetCurrentUserId();
        if (userId.HasValue)
            await _auditLog.LogAsync(userId.Value, "BoxRuns", boxRunId, "Cancel");

        return Ok(new ApiResponse<object>
        {
            Success = true,
            Data = new { BoxRunId = boxRunId },
            Message = "Cancellation requested. Running tasks will finish; no new pending tasks will be scheduled."
        });
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
            CreatedAt = box.CreatedAtUtc,
            NotificationEmail = box.NotificationEmail,
            DepartmentId = box.DepartmentId,
            DepartmentName = box.DepartmentName
        };
        if (includeTasks)
        {
            var tasks = await _taskRepository.GetTasksForBoxAsync(box.Id);
            var dependenciesByTaskId = await _taskRepository.GetDependenciesForBoxAsync(box.Id);

            foreach (var t in tasks)
            {
                dependenciesByTaskId.TryGetValue(t.Id, out var deps);
                dto.Tasks.Add(new TaskDto
                {
                    TaskId = t.Id, BoxId = t.BoxId, Name = t.Name, Description = t.Description,
                    Command = t.Command, TaskType = t.TaskType.ToString(),
                    Enabled = t.Enabled, CreatedAt = t.CreatedAtUtc,
                    DependencyTaskIds = deps ?? []
                });
            }
        }
        return dto;
    }

    private static bool TryValidateEmail(string? email, out string error)
    {
        error = "";
        if (string.IsNullOrWhiteSpace(email))
            return true; // Email is optional
        
        try
        {
            var addr = new System.Net.Mail.MailAddress(email);
            return addr.Address == email;
        }
        catch
        {
            error = "Notification email format is invalid.";
            return false;
        }
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

    /// <summary>
    /// Gets the department ID from the current user's JWT claims.
    /// Returns null if the claim is missing (for backward compatibility with non-department users).
    /// </summary>
    private int? GetCurrentDepartmentId()
    {
        var claim = User.FindFirst("department_id");
        return claim != null && int.TryParse(claim.Value, out var id) ? id : null;
    }
}
