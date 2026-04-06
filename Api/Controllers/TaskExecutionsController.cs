using CHRONIQ.Api.Dtos;
using CHRONIQ.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace CHRONIQ.Api.Controllers;

[ApiController]
[Route("api/task-executions")]
[Authorize]
public class TaskExecutionsController : ControllerBase
{
    private readonly IExecutionRepository _executionRepository;
    private readonly IBoxRepository _boxRepository;

    public TaskExecutionsController(IExecutionRepository executionRepository, IBoxRepository boxRepository)
    {
        ArgumentNullException.ThrowIfNull(executionRepository);
        ArgumentNullException.ThrowIfNull(boxRepository);
        _executionRepository = executionRepository;
        _boxRepository = boxRepository;
    }

    [HttpGet("{taskExecutionId}/logs")]
    [Authorize(Roles = "Admin,Operator,Viewer")]
    public async Task<IActionResult> GetLogs(int taskExecutionId)
    {
        var execution = await _executionRepository.GetExecutionByIdAsync(taskExecutionId);
        if (execution == null)
        {
            return NotFound(new ApiResponse<object>
            {
                Success = false,
                Message = "Task execution not found.",
                ErrorCode = "TASK_EXECUTION_NOT_FOUND"
            });
        }

        var box = await _boxRepository.GetByIdAsync(execution.BoxId);
        if (box == null)
        {
            return NotFound(new ApiResponse<object>
            {
                Success = false,
                Message = "Box not found.",
                ErrorCode = "BOX_NOT_FOUND"
            });
        }

        var userDepartmentId = GetCurrentDepartmentId();
        if (userDepartmentId.HasValue && box.DepartmentId.HasValue && box.DepartmentId != userDepartmentId)
        {
            return Forbid("You do not have permission to access this task execution.");
        }

        var logs = await _executionRepository.GetLogsByTaskExecutionIdAsync(taskExecutionId);
        var dtos = logs.Select(log => new TaskExecutionLogDto
        {
            Id = log.Id,
            BoxRunId = log.BoxRunId,
            TaskId = log.TaskId,
            TaskExecutionId = log.TaskExecutionId,
            Timestamp = log.Timestamp,
            Level = log.Level,
            Message = log.Message,
            Details = log.Details
        }).ToList();

        return Ok(new ApiResponse<List<TaskExecutionLogDto>> { Success = true, Data = dtos });
    }

    private int? GetCurrentDepartmentId()
    {
        var claim = User?.FindFirst("department_id");
        return claim != null && int.TryParse(claim.Value, out var id) ? id : null;
    }
}