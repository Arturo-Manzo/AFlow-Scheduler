using AScheduler.Api.Dtos;
using AScheduler.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AScheduler.Api.Controllers;

[ApiController]
[Route("api/task-executions")]
[Authorize]
public class TaskExecutionsController : ControllerBase
{
    private readonly IExecutionRepository _executionRepository;

    public TaskExecutionsController(IExecutionRepository executionRepository)
    {
        ArgumentNullException.ThrowIfNull(executionRepository);
        _executionRepository = executionRepository;
    }

    [HttpGet("{taskExecutionId}/logs")]
    [Authorize(Roles = "Admin,Operator,Viewer")]
    public async Task<IActionResult> GetLogs(int taskExecutionId)
    {
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
}