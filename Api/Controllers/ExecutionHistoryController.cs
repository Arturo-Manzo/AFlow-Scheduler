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

    public ExecutionHistoryController(IExecutionRepository executionRepository)
    {
        ArgumentNullException.ThrowIfNull(executionRepository);
        _executionRepository = executionRepository;
    }

    [HttpGet("task/{taskId}")]
    [Authorize(Roles = "Admin,Operator,Viewer")]
    public async Task<IActionResult> GetForTask(int taskId)
    {
        var records = await _executionRepository.GetExecutionsForTaskAsync(taskId);
        var dtos = records.Select(MapToDto).ToList();
        return Ok(new ApiResponse<List<ExecutionDto>> { Success = true, Data = dtos });
    }

    [HttpGet("boxrun/{boxRunId}")]
    [Authorize(Roles = "Admin,Operator,Viewer")]
    public async Task<IActionResult> GetForBoxRun(int boxRunId)
    {
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
        var dtos = records.Select(MapToDto).ToList();
        return Ok(new ApiResponse<List<ExecutionDto>> { Success = true, Data = dtos });
    }

    private static ExecutionDto MapToDto(ExecutionRepository.ExecutionRecord r) => new()
    {
        ExecutionId = r.ExecutionId,
        TaskId = r.TaskId,
        TaskName = r.TaskName,
        BoxId = r.BoxId,
        BoxName = r.BoxName,
        BoxRunId = r.BoxRunId,
        StartedAt = r.StartedAt,
        EndedAt = r.EndedAt,
        Status = r.Status,
        ExitCode = r.ExitCode,
        StdOut = r.StdOut,
        StdErr = r.StdErr,
        DurationSeconds = r.EndedAt.HasValue ? (int?)(r.EndedAt.Value - r.StartedAt).TotalSeconds : null,
        TriggerSource = r.TriggerSource
    };
}
