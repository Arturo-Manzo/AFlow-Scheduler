using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AScheduler.Api.Dtos;
using AScheduler.Api.Services;
using AScheduler.Data;
using AScheduler.Domain;
using AScheduler.Queue;
using AScheduler.Services;

namespace AScheduler.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class TasksController : ControllerBase
{
    private readonly ITaskRepository _taskRepository;
    private readonly IAuditLogService _auditLog;
    private readonly ITaskQueue _queue;
    private readonly IWorkerStateService _workerState;
    private readonly ILogger<TasksController> _logger;

    public TasksController(ITaskRepository taskRepository, IAuditLogService auditLog, ITaskQueue queue, IWorkerStateService workerState, ILogger<TasksController> logger)
    {
        ArgumentNullException.ThrowIfNull(taskRepository);
        ArgumentNullException.ThrowIfNull(auditLog);
        ArgumentNullException.ThrowIfNull(queue);
        ArgumentNullException.ThrowIfNull(workerState);
        ArgumentNullException.ThrowIfNull(logger);
        _taskRepository = taskRepository;
        _auditLog = auditLog;
        _queue = queue;
        _workerState = workerState;
        _logger = logger;
    }

    [HttpGet]
    [Authorize(Roles = "Admin,Operator,Viewer")]
    public async Task<IActionResult> GetForBox([FromQuery] int boxId)
    {
        var tasks = await _taskRepository.GetTasksForBoxAsync(boxId);
        var dtos = new List<TaskDto>();
        foreach (var t in tasks)
        {
            var deps = await _taskRepository.GetTaskDependenciesAsync(t.Id);
            dtos.Add(MapToDto(t, deps));
        }
        return Ok(new ApiResponse<List<TaskDto>> { Success = true, Data = dtos });
    }

    [HttpGet("{taskId}")]
    [Authorize(Roles = "Admin,Operator,Viewer")]
    public async Task<IActionResult> GetById(int taskId)
    {
        var task = await _taskRepository.GetByIdAsync(taskId);
        if (task == null)
            return NotFound(new ApiResponse<object> { Success = false, Message = "Task not found.", ErrorCode = "TASK_NOT_FOUND" });
        var deps = await _taskRepository.GetTaskDependenciesAsync(taskId);
        return Ok(new ApiResponse<TaskDto> { Success = true, Data = MapToDto(task, deps) });
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create([FromBody] CreateTaskRequest request)
    {
        if (request.BoxId <= 0 || string.IsNullOrWhiteSpace(request.Name) ||
            string.IsNullOrWhiteSpace(request.Command) || string.IsNullOrWhiteSpace(request.TaskType))
            return BadRequest(new ApiResponse<object> { Success = false, Message = "Missing required fields.", ErrorCode = "MISSING_FIELDS" });

        var validation = await ValidateDependenciesAsync(request.BoxId, null, request.DependencyTaskIds);
        if (!validation.Success)
            return BadRequest(new ApiResponse<object> { Success = false, Message = validation.Message, ErrorCode = validation.ErrorCode });

        try
        {
            var taskId = await _taskRepository.CreateAsync(
                request.BoxId, request.Name, request.Description,
                request.Command, request.TaskType);

            await _taskRepository.ReplaceTaskDependenciesAsync(taskId, request.DependencyTaskIds);

            var userId = GetCurrentUserId();
            if (userId.HasValue)
                await _auditLog.LogAsync(userId.Value, "Tasks", taskId, "Create", newValues: request.Name);

            var task = await _taskRepository.GetByIdAsync(taskId);
            var deps = await _taskRepository.GetTaskDependenciesAsync(taskId);
            return CreatedAtAction(nameof(GetById), new { taskId }, new ApiResponse<TaskDto> { Success = true, Data = MapToDto(task!, deps) });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating task.");
            return BadRequest(new ApiResponse<object> { Success = false, Message = "Error creating task.", ErrorCode = "CREATION_ERROR" });
        }
    }

    [HttpPut("{taskId}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Update(int taskId, [FromBody] UpdateTaskRequest request)
    {
        var existing = await _taskRepository.GetByIdAsync(taskId);
        if (existing == null)
            return NotFound(new ApiResponse<object> { Success = false, Message = "Task not found.", ErrorCode = "TASK_NOT_FOUND" });

        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Command))
            return BadRequest(new ApiResponse<object> { Success = false, Message = "Missing required fields.", ErrorCode = "MISSING_FIELDS" });

        var validation = await ValidateDependenciesAsync(existing.BoxId, taskId, request.DependencyTaskIds);
        if (!validation.Success)
            return BadRequest(new ApiResponse<object> { Success = false, Message = validation.Message, ErrorCode = validation.ErrorCode });

        await _taskRepository.UpdateAsync(taskId, request.Name, request.Description,
            request.Command, request.TaskType, request.Enabled);

        await _taskRepository.ReplaceTaskDependenciesAsync(taskId, request.DependencyTaskIds);

        var userId = GetCurrentUserId();
        if (userId.HasValue)
            await _auditLog.LogAsync(userId.Value, "Tasks", taskId, "Update", newValues: request.Name);

        var task = await _taskRepository.GetByIdAsync(taskId);
        var deps = await _taskRepository.GetTaskDependenciesAsync(taskId);
        return Ok(new ApiResponse<TaskDto> { Success = true, Data = MapToDto(task!, deps) });
    }

    [HttpDelete("{taskId}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int taskId)
    {
        var existing = await _taskRepository.GetByIdAsync(taskId);
        if (existing == null)
            return NotFound(new ApiResponse<object> { Success = false, Message = "Task not found.", ErrorCode = "TASK_NOT_FOUND" });

        await _taskRepository.DeleteAsync(taskId);

        var userId = GetCurrentUserId();
        if (userId.HasValue)
            await _auditLog.LogAsync(userId.Value, "Tasks", taskId, "Delete");

        return Ok(new ApiResponse<object> { Success = true, Message = "Task deleted." });
    }

    /// <summary>
    /// Executes a single task immediately and in isolation.
    /// - Dependencies are always ignored.
    /// - Does NOT create or affect any BoxRun.
    /// - Rejects the request if the same task is already queued or running.
    /// </summary>
    [HttpPost("{taskId}/force-start")]
    [Authorize(Roles = "Admin,Operator")]
    public async Task<IActionResult> ForceStart(int taskId, [FromBody] ForceStartTaskRequest? request)
    {
        var task = await _taskRepository.GetByIdAsync(taskId);
        if (task == null)
            return NotFound(new ApiResponse<object> { Success = false, Message = "Task not found.", ErrorCode = "TASK_NOT_FOUND" });

        if (string.IsNullOrWhiteSpace(request?.Reason))
            return BadRequest(new ApiResponse<object> { Success = false, Message = "Reason is required.", ErrorCode = "REASON_REQUIRED" });

        var userId = GetCurrentUserId();

        if (_workerState.IsTaskRunning(taskId))
            return Conflict(new ApiResponse<object>
            {
                Success = false,
                Message = "Task is already running.",
                ErrorCode = "TASK_ALREADY_RUNNING"
            });

        var enqueued = await _queue.EnqueueForceStartAsync(new TaskForceStartRequest
        {
            TaskId = taskId,
            RequestedByUserId = userId,
            Reason = request?.Reason ?? "",
            RequestedAtUtc = DateTime.UtcNow
        });

        if (!enqueued)
            return Conflict(new ApiResponse<object>
            {
                Success = false,
                Message = "Task is already queued or running.",
                ErrorCode = "TASK_ALREADY_QUEUED"
            });

        if (userId.HasValue)
            await _auditLog.LogAsync(userId.Value, "Tasks", taskId, "ForceStart", newValues: task.Name);

        return Accepted(new ApiResponse<object> { Success = true, Message = $"Task '{task.Name}' accepted for immediate execution." });
    }

    private static TaskDto MapToDto(TaskDefinition t, List<int> deps) => new()
    {
        TaskId = t.Id, BoxId = t.BoxId, Name = t.Name, Description = t.Description,
        Command = t.Command, TaskType = t.TaskType.ToString(),
        Enabled = t.Enabled, CreatedAt = t.CreatedAtUtc,
        DependencyTaskIds = deps
    };

    private async Task<(bool Success, string Message, string ErrorCode)> ValidateDependenciesAsync(int boxId, int? taskId, IEnumerable<int>? dependencyTaskIds)
    {
        var distinct = (dependencyTaskIds ?? []).Distinct().Where(id => id > 0).ToList();

        if (taskId.HasValue && distinct.Contains(taskId.Value))
            return (false, "A task cannot depend on itself.", "SELF_DEPENDENCY");

        var boxTasks = await _taskRepository.GetTasksForBoxAsync(boxId);
        var boxTaskIds = boxTasks.Select(t => t.Id).ToHashSet();

        foreach (var depId in distinct)
        {
            if (!boxTaskIds.Contains(depId))
                return (false, "Dependencies must reference active tasks in the same box.", "INVALID_DEPENDENCY");
        }

        if (!taskId.HasValue)
            return (true, string.Empty, string.Empty);

        var dependencyGraph = new Dictionary<int, List<int>>();
        foreach (var task in boxTasks)
        {
            var deps = await _taskRepository.GetTaskDependenciesAsync(task.Id);
            dependencyGraph[task.Id] = deps;
        }

        dependencyGraph[taskId.Value] = distinct;

        foreach (var depId in distinct)
        {
            if (HasPath(depId, taskId.Value, dependencyGraph, new HashSet<int>()))
                return (false, "Circular dependency detected.", "CIRCULAR_DEPENDENCY");
        }

        return (true, string.Empty, string.Empty);
    }

    private static bool HasPath(int startTaskId, int targetTaskId, IReadOnlyDictionary<int, List<int>> graph, HashSet<int> visited)
    {
        if (startTaskId == targetTaskId)
            return true;

        if (!visited.Add(startTaskId))
            return false;

        if (!graph.TryGetValue(startTaskId, out var deps) || deps.Count == 0)
            return false;

        foreach (var dep in deps)
        {
            if (HasPath(dep, targetTaskId, graph, visited))
                return true;
        }

        return false;
    }

    private int? GetCurrentUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier) ?? User.FindFirst("sub") ?? User.FindFirst("userId");
        return claim != null && int.TryParse(claim.Value, out var id) ? id : null;
    }
}
