using AScheduler.Api.Dtos;
using AScheduler.Data;
using AScheduler.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AScheduler.Api.Controllers;

[ApiController]
[Route("api/box-runs")]
[Authorize]
public class BoxRunsController : ControllerBase
{
    private readonly IBoxRepository _boxRepository;
    private readonly ITaskRepository _taskRepository;
    private readonly IExecutionRepository _executionRepository;
    private readonly IBoxRunMetricsService _boxRunMetricsService;

    public BoxRunsController(
        IBoxRepository boxRepository,
        ITaskRepository taskRepository,
        IExecutionRepository executionRepository,
        IBoxRunMetricsService boxRunMetricsService)
    {
        ArgumentNullException.ThrowIfNull(boxRepository);
        ArgumentNullException.ThrowIfNull(taskRepository);
        ArgumentNullException.ThrowIfNull(executionRepository);
        ArgumentNullException.ThrowIfNull(boxRunMetricsService);
        _boxRepository = boxRepository;
        _taskRepository = taskRepository;
        _executionRepository = executionRepository;
        _boxRunMetricsService = boxRunMetricsService;
    }

    [HttpGet]
    [Authorize(Roles = "Admin,Operator,Viewer")]
    public async Task<IActionResult> GetAll([FromQuery] int limit = 100)
    {
        var runs = await _boxRepository.GetRecentBoxRunsAsync(limit);
        var dtos = runs.Select(MapToBoxRunDto).ToList();
        return Ok(new ApiResponse<List<BoxRunDto>> { Success = true, Data = dtos });
    }

    [HttpGet("{boxRunId}")]
    [Authorize(Roles = "Admin,Operator,Viewer")]
    public async Task<IActionResult> GetById(int boxRunId)
    {
        var run = await _boxRepository.GetBoxRunSummaryAsync(boxRunId);
        if (run == null)
            return NotFound(new ApiResponse<object> { Success = false, Message = "BoxRun not found.", ErrorCode = "BOXRUN_NOT_FOUND" });

        return Ok(new ApiResponse<BoxRunDto> { Success = true, Data = MapToBoxRunDto(run) });
    }

    [HttpGet("{boxRunId}/tasks")]
    [Authorize(Roles = "Admin,Operator,Viewer")]
    public async Task<IActionResult> GetTasks(int boxRunId)
    {
        var run = await _boxRepository.GetBoxRunAsync(boxRunId);
        if (run == null)
            return NotFound(new ApiResponse<object> { Success = false, Message = "BoxRun not found.", ErrorCode = "BOXRUN_NOT_FOUND" });

        var tasks = await _taskRepository.GetTasksForBoxAsync(run.BoxId);
        var records = await _executionRepository.GetExecutionsForBoxRunAsync(boxRunId);

        var latestByTaskId = records
            .GroupBy(r => r.TaskId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(r => r.ExecutionId).First());

        var dtos = new List<BoxRunTaskExecutionDto>(tasks.Count);
        foreach (var task in tasks.OrderBy(t => t.Id))
        {
            var deps = await _taskRepository.GetTaskDependenciesAsync(task.Id);
            latestByTaskId.TryGetValue(task.Id, out var execution);

            var status = execution == null
                ? "Pending"
                : execution.Status switch
                {
                    "NotExecuted" => "Skipped",
                    "Aborted" => "Skipped",
                    _ => execution.Status
                };

            dtos.Add(new BoxRunTaskExecutionDto
            {
                ExecutionId = execution?.ExecutionId,
                TaskId = task.Id,
                Name = task.Name,
                Status = status,
                StartTime = execution?.StartedAt,
                EndTime = execution?.EndedAt,
                DurationSeconds = execution?.EndedAt.HasValue == true
                    ? (int?)(execution.EndedAt.Value - execution.StartedAt).TotalSeconds
                    : null,
                Error = execution?.Error,
                StackTrace = execution?.StdErr,
                DependsOn = deps
            });
        }

        return Ok(new ApiResponse<List<BoxRunTaskExecutionDto>> { Success = true, Data = dtos });
    }

    [HttpGet("{boxRunId}/logs")]
    [Authorize(Roles = "Admin,Operator,Viewer")]
    public async Task<IActionResult> GetLogs(int boxRunId)
    {
        var run = await _boxRepository.GetBoxRunAsync(boxRunId);
        if (run == null)
            return NotFound(new ApiResponse<object> { Success = false, Message = "BoxRun not found.", ErrorCode = "BOXRUN_NOT_FOUND" });

        var logs = await _executionRepository.GetLogsByBoxRunIdAsync(boxRunId);
        var dtos = logs.Select(MapToTaskExecutionLogDto).ToList();
        return Ok(new ApiResponse<List<TaskExecutionLogDto>> { Success = true, Data = dtos });
    }

    [HttpGet("{boxRunId}/metrics")]
    [Authorize(Roles = "Admin,Operator,Viewer")]
    public async Task<IActionResult> GetMetrics(int boxRunId)
    {
        var metrics = await _boxRunMetricsService.GetMetricsAsync(boxRunId);
        if (metrics == null)
            return NotFound(new ApiResponse<object> { Success = false, Message = "BoxRun not found.", ErrorCode = "BOXRUN_NOT_FOUND" });

        return Ok(new ApiResponse<BoxRunMetricsDto> { Success = true, Data = metrics });
    }

    private static BoxRunDto MapToBoxRunDto(Domain.BoxRunSummary run)
    {
        var start = run.StartedAtUtc ?? run.CreatedAtUtc;
        return new BoxRunDto
        {
            Id = run.BoxRunId,
            BoxId = run.BoxId,
            BoxName = run.BoxName,
            Status = run.Status,
            IsCancellationRequested = run.IsCancelled,
            StartTime = start,
            EndTime = run.EndedAtUtc,
            ScheduledForUtc = run.ScheduledForUtc,
            TriggerSource = run.TriggerSource,
            DurationSeconds = run.EndedAtUtc.HasValue ? (int?)(run.EndedAtUtc.Value - start).TotalSeconds : null
        };
    }

    private static TaskExecutionLogDto MapToTaskExecutionLogDto(Domain.TaskExecutionLog log) => new()
    {
        Id = log.Id,
        BoxRunId = log.BoxRunId,
        TaskId = log.TaskId,
        TaskExecutionId = log.TaskExecutionId,
        Timestamp = log.Timestamp,
        Level = log.Level,
        Message = log.Message,
        Details = log.Details
    };
}
