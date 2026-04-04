using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AScheduler.Api.Dtos;
using AScheduler.Data;

namespace AScheduler.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ExecutionHistoryController : ControllerBase
{
    private readonly IExecutionRepository _executionRepository;
    private readonly ITaskRepository _taskRepository;
    private readonly IBoxRepository _boxRepository;
    private readonly IConfiguration _configuration;

    public ExecutionHistoryController(IExecutionRepository executionRepository, ITaskRepository taskRepository, IBoxRepository boxRepository, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(executionRepository);
        ArgumentNullException.ThrowIfNull(taskRepository);
        ArgumentNullException.ThrowIfNull(boxRepository);
        ArgumentNullException.ThrowIfNull(configuration);
        _executionRepository = executionRepository;
        _taskRepository = taskRepository;
        _boxRepository = boxRepository;
        _configuration = configuration;
    }

    [HttpGet("task/{taskId}")]
    [Authorize(Roles = "Admin,Operator,Viewer")]
    public async Task<IActionResult> GetForTask(int taskId, [FromQuery] DateTime? fromUtc = null, [FromQuery] DateTime? toUtc = null)
    {
        var task = await _taskRepository.GetByIdAsync(taskId);
        if (task == null)
            return NotFound(new ApiResponse<object> { Success = false, Message = "Task not found.", ErrorCode = "TASK_NOT_FOUND" });

        // Authorization: Check if user can access this task's box's department
        var box = await _boxRepository.GetByIdAsync(task.BoxId);
        if (box == null)
            return NotFound(new ApiResponse<object> { Success = false, Message = "Box not found.", ErrorCode = "BOX_NOT_FOUND" });

        var userDepartmentId = GetCurrentDepartmentId();
        if (userDepartmentId.HasValue && box.DepartmentId.HasValue && box.DepartmentId != userDepartmentId)
            return Forbid("You do not have permission to access this task.");

        var records = await _executionRepository.GetExecutionsForTaskAsync(taskId, fromUtc, toUtc);
        var dtos = records.Select(MapToDto).ToList();
        return Ok(new ApiResponse<List<ExecutionDto>> { Success = true, Data = dtos });
    }

    [HttpGet("task/{taskId}/last")]
    [Authorize(Roles = "Admin,Operator,Viewer")]
    public async Task<IActionResult> GetLastForTask(int taskId)
    {
        var task = await _taskRepository.GetByIdAsync(taskId);
        if (task == null)
            return NotFound(new ApiResponse<object> { Success = false, Message = "Task not found.", ErrorCode = "TASK_NOT_FOUND" });

        var box = await _boxRepository.GetByIdAsync(task.BoxId);
        if (box == null)
            return NotFound(new ApiResponse<object> { Success = false, Message = "Box not found.", ErrorCode = "BOX_NOT_FOUND" });

        var userDepartmentId = GetCurrentDepartmentId();
        if (userDepartmentId.HasValue && box.DepartmentId.HasValue && box.DepartmentId != userDepartmentId)
            return Forbid("You do not have permission to access this task.");

        var record = await _executionRepository.GetLastExecutionForTaskAsync(taskId);
        if (record == null)
            return NotFound(new ApiResponse<object> { Success = false, Message = "No execution history found for this task.", ErrorCode = "NO_EXECUTION_HISTORY" });

        return Ok(new ApiResponse<ExecutionDto> { Success = true, Data = MapToDto(record) });
    }

    [HttpGet("running")]
    [Authorize(Roles = "Admin,Operator,Viewer")]
    public async Task<IActionResult> GetRunning()
    {
        var staleMinutes = Math.Max(1, _configuration.GetValue<int>("WorkerPool:StaleExecutionThresholdMinutes", 15));
        var staleBeforeUtc = DateTime.UtcNow.AddMinutes(-staleMinutes);
        var records = await _executionRepository.GetRunningExecutionsAsync(staleBeforeUtc);
        records = await FilterRecordsByDepartmentAsync(records);
        var dtos = records.Select(MapToDto).ToList();
        return Ok(new ApiResponse<List<RunningExecutionDto>>
        {
            Success = true,
            Data = dtos.Select(dto => new RunningExecutionDto
            {
                ExecutionId = dto.ExecutionId,
                TaskId = dto.TaskId,
                TaskName = dto.TaskName,
                BoxId = dto.BoxId,
                BoxName = dto.BoxName,
                BoxTimeZoneId = dto.BoxTimeZoneId,
                BoxRunId = dto.BoxRunId,
                StartedAt = dto.StartedAt,
                EndedAt = dto.EndedAt,
                Status = dto.Status,
                ExitCode = dto.ExitCode,
                StdOut = dto.StdOut,
                StdErr = dto.StdErr,
                DurationSeconds = dto.DurationSeconds,
                TriggerSource = dto.TriggerSource,
                Reason = dto.Reason,
                RequestedByUserId = dto.RequestedByUserId,
                RequestedByUsername = dto.RequestedByUsername,
                ErrorMessage = dto.ErrorMessage,
                IsStale = dto.IsStale
            }).ToList()
        });
    }

    [HttpGet("boxrun/{boxRunId}")]
    [Authorize(Roles = "Admin,Operator,Viewer")]
    public async Task<IActionResult> GetForBoxRun(int boxRunId)
    {
        var boxRun = await _boxRepository.GetBoxRunAsync(boxRunId);
        if (boxRun == null)
            return NotFound(new ApiResponse<object> { Success = false, Message = "BoxRun not found.", ErrorCode = "BOXRUN_NOT_FOUND" });

        var box = await _boxRepository.GetByIdAsync(boxRun.BoxId);
        if (box == null)
            return NotFound(new ApiResponse<object> { Success = false, Message = "Box not found.", ErrorCode = "BOX_NOT_FOUND" });

        var userDepartmentId = GetCurrentDepartmentId();
        if (userDepartmentId.HasValue && box.DepartmentId.HasValue && box.DepartmentId != userDepartmentId)
            return Forbid("You do not have permission to access this box run.");

        var records = await _executionRepository.GetExecutionsForBoxRunAsync(boxRunId);
        var dtos = records.Select(MapToDto).ToList();
        return Ok(new ApiResponse<List<ExecutionDto>> { Success = true, Data = dtos });
    }

    [HttpGet("latest")]
    [HttpGet("/api/logs/latest")]
    [Authorize(Roles = "Admin,Operator,Viewer")]
    public async Task<IActionResult> GetLatest([FromQuery] int limit = 20)
    {
        if (limit <= 0) limit = 20;
        var records = await _executionRepository.GetLatestExecutionsAsync(limit);
        records = await FilterRecordsByDepartmentAsync(records);
        var dtos = records.Select(MapToDto).ToList();
        return Ok(new ApiResponse<List<ExecutionDto>> { Success = true, Data = dtos });
    }

    [HttpGet("failed")]
    [Authorize(Roles = "Admin,Operator,Viewer")]
    public async Task<IActionResult> GetFailed(
        [FromQuery] int limit = 50,
        [FromQuery] int? boxId = null,
        [FromQuery] DateTime? fromUtc = null,
        [FromQuery] DateTime? toUtc = null,
        [FromQuery] string[]? status = null,
        [FromQuery] string? taskName = null,
        [FromQuery] string? triggerSource = null)
    {
        if (limit <= 0) limit = 50;

        // Non-admins only see their own department's failures
        var userDepartmentId = GetCurrentDepartmentId();
        if (userDepartmentId.HasValue && boxId.HasValue)
        {
            var box = await _boxRepository.GetByIdAsync(boxId.Value);
            if (box != null && box.DepartmentId.HasValue && box.DepartmentId != userDepartmentId)
                return Forbid("You do not have permission to access this box.");
        }

        var records = await _executionRepository.GetFailedExecutionsAsync(limit, boxId, fromUtc, toUtc, userDepartmentId, status, taskName, triggerSource);
        var dtos = records.Select(MapToDto).ToList();
        return Ok(new ApiResponse<List<ExecutionDto>> { Success = true, Data = dtos });
    }

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

    /// <summary>
    /// Gets the department ID from the current user's JWT claims.
    /// Returns null if the claim is missing (for backward compatibility with non-department users).
    /// </summary>
    private int? GetCurrentDepartmentId()
    {
        var claim = User?.FindFirst("department_id");
        return claim != null && int.TryParse(claim.Value, out var id) ? id : null;
    }

    private async Task<List<ExecutionRepository.ExecutionRecord>> FilterRecordsByDepartmentAsync(List<ExecutionRepository.ExecutionRecord> records)
    {
        var userDepartmentId = GetCurrentDepartmentId();
        if (!userDepartmentId.HasValue)
            return records;

        var accessCache = new Dictionary<int, bool>();
        var filtered = new List<ExecutionRepository.ExecutionRecord>(records.Count);

        foreach (var record in records)
        {
            if (!accessCache.TryGetValue(record.BoxId, out var hasAccess))
            {
                var box = await _boxRepository.GetByIdAsync(record.BoxId);
                hasAccess = box == null || !box.DepartmentId.HasValue || box.DepartmentId == userDepartmentId.Value;
                accessCache[record.BoxId] = hasAccess;
            }

            if (hasAccess)
                filtered.Add(record);
        }

        return filtered;
    }
}
