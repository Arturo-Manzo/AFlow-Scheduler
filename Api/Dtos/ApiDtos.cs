namespace AScheduler.Api.Dtos;

// ============================================
// Auth DTOs
// ============================================

/// <summary>
/// Login request payload.
/// </summary>
public class LoginRequest
{
    /// <summary>
    /// The username for authentication.
    /// </summary>
    public string Username { get; set; } = "";

    /// <summary>
    /// The password for authentication.
    /// </summary>
    public string Password { get; set; } = "";
}

/// <summary>
/// Login response containing JWT token.
/// </summary>
public class LoginResponse
{
    /// <summary>
    /// The JWT access token for API requests.
    /// </summary>
    public string AccessToken { get; set; } = "";

    /// <summary>
    /// The user information.
    /// </summary>
    public UserDto User { get; set; } = new();
}

// ============================================
// User DTOs
// ============================================

/// <summary>
/// Data transfer object for user information.
/// </summary>
public class UserDto
{
    /// <summary>
    /// Unique user identifier.
    /// </summary>
    public int UserId { get; set; }

    /// <summary>
    /// The username.
    /// </summary>
    public string Username { get; set; } = "";

    /// <summary>
    /// User email address.
    /// </summary>
    public string Email { get; set; } = "";

    /// <summary>
    /// User's role ID.
    /// </summary>
    public int RoleId { get; set; }

    /// <summary>
    /// User's role name.
    /// </summary>
    public string RoleName { get; set; } = "";

    /// <summary>
    /// Whether the user account is active.
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// Timestamp of account creation.
    /// </summary>
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Request to create a new user.
/// </summary>
public class CreateUserRequest
{
    /// <summary>
    /// Username (must be unique).
    /// </summary>
    public string Username { get; set; } = "";

    /// <summary>
    /// Email address.
    /// </summary>
    public string Email { get; set; } = "";

    /// <summary>
    /// Plain text password (will be hashed).
    /// </summary>
    public string Password { get; set; } = "";

    /// <summary>
    /// Role ID (1=Admin, 2=Operator, 3=Viewer).
    /// </summary>
    public int RoleId { get; set; }
}

/// <summary>
/// Request to update user information.
/// </summary>
public class UpdateUserRequest
{
    /// <summary>
    /// Email address.
    /// </summary>
    public string Email { get; set; } = "";

    /// <summary>
    /// Role ID.
    /// </summary>
    public int RoleId { get; set; }

    /// <summary>
    /// Whether the account is active.
    /// </summary>
    public bool IsActive { get; set; }
}

/// <summary>
/// Request to change the current user's password.
/// </summary>
public class ChangePasswordRequest
{
    /// <summary>
    /// Current password for validation.
    /// </summary>
    public string CurrentPassword { get; set; } = "";

    /// <summary>
    /// New password to store.
    /// </summary>
    public string NewPassword { get; set; } = "";
}

// ============================================
// Box DTOs
// ============================================

public class BoxDto
{
    public int BoxId { get; set; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string CronExpression { get; set; } = "";
    public string TimeZoneId { get; set; } = "Etc/UTC";
    public bool Enabled { get; set; }
    public DateTime? LastRunUtc { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<TaskDto> Tasks { get; set; } = new();
}

public class InitialTaskRequest
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string Command { get; set; } = "";
    public string TaskType { get; set; } = "Exe";
}

public class CreateBoxRequest
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string CronExpression { get; set; } = "";
    public string TimeZoneId { get; set; } = "";
    public InitialTaskRequest InitialTask { get; set; } = new();
}

public class UpdateBoxRequest
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string CronExpression { get; set; } = "";
    public string TimeZoneId { get; set; } = "";
    public bool Enabled { get; set; }
}

public class ExecuteBoxRequest
{
    public bool IgnoreDependencies { get; set; }
    public bool IgnoreSchedule { get; set; }
    public string Reason { get; set; } = "";
}

// ============================================
// Task DTOs
// ============================================

public class TaskDto
{
    public int TaskId { get; set; }
    public int BoxId { get; set; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string Command { get; set; } = "";
    public string TaskType { get; set; } = "";
    public bool Enabled { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<int> DependencyTaskIds { get; set; } = new();
}

public class CreateTaskRequest
{
    public int BoxId { get; set; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string Command { get; set; } = "";
    public string TaskType { get; set; } = "";
    public List<int> DependencyTaskIds { get; set; } = new();
}

public class UpdateTaskRequest
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string Command { get; set; } = "";
    public string TaskType { get; set; } = "";
    public bool Enabled { get; set; }
    public List<int> DependencyTaskIds { get; set; } = new();
}

public class TaskListResponse : PaginatedResponse<TaskDto>
{
}

// ============================================
// Execution DTOs
// ============================================

public class ExecuteTaskRequest
{
    public bool IgnoreDependencies { get; set; }
    public bool IgnoreSchedule { get; set; }
    public string Reason { get; set; } = "";
}

/// <summary>
/// Request body for POST /api/tasks/{taskId}/force-start.
/// Dependencies are always ignored for force-start — no flag needed.
/// </summary>
public class ForceStartTaskRequest
{
    public string Reason { get; set; } = "";
}

public class ExecutionDto
{
    public int ExecutionId { get; set; }
    public int TaskId { get; set; }
    public string TaskName { get; set; } = "";
    public int BoxId { get; set; }
    public string BoxName { get; set; } = "";
    public string BoxTimeZoneId { get; set; } = "Etc/UTC";
    public int? BoxRunId { get; set; }   // NULL for ForceStart executions
    public DateTime StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public string Status { get; set; } = "";
    public int? ExitCode { get; set; }
    public string StdOut { get; set; } = "";
    public string StdErr { get; set; } = "";
    public int? DurationSeconds { get; set; }
    public string TriggerSource { get; set; } = AScheduler.Domain.TriggerSources.Scheduler;
    public string? Reason { get; set; }
    public int? RequestedByUserId { get; set; }
    public string? RequestedByUsername { get; set; }
    public string? ErrorMessage { get; set; }
    public bool IsStale { get; set; }
}

public class RunningExecutionDto : ExecutionDto
{
}

public class BoxRunDto
{
    public int Id { get; set; }
    public int BoxId { get; set; }
    public string BoxName { get; set; } = "";
    public string Status { get; set; } = "Pending";
    public bool IsCancellationRequested { get; set; }
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public DateTime? ScheduledForUtc { get; set; }
    public string TriggerSource { get; set; } = AScheduler.Domain.TriggerSources.Scheduler;
    public int? DurationSeconds { get; set; }
}

public class BoxRunMetricsDto
{
    public int BoxRunId { get; set; }
    public int TotalTasks { get; set; }
    public int SuccessCount { get; set; }
    public int FailedCount { get; set; }
    public int PendingCount { get; set; }
    public TimeSpan? TotalDuration { get; set; }
    public int? TotalDurationSeconds { get; set; }
    public double SuccessRate { get; set; }
    public List<TaskMetricDto> Tasks { get; set; } = new();
}

public class TaskMetricDto
{
    public int TaskId { get; set; }
    public string Name { get; set; } = "";
    public string Status { get; set; } = "Pending";
    public TimeSpan? Duration { get; set; }
    public int? DurationSeconds { get; set; }
}

public class BoxRunTaskExecutionDto
{
    public int? ExecutionId { get; set; }
    public int TaskId { get; set; }
    public string Name { get; set; } = "";
    public string Status { get; set; } = "Pending";
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public int? DurationSeconds { get; set; }
    public string? Error { get; set; }
    public string? StackTrace { get; set; }
    public List<int> DependsOn { get; set; } = new();
}

public class TaskExecutionLogDto
{
    public Guid Id { get; set; }
    public int? BoxRunId { get; set; }
    public int TaskId { get; set; }
    public int TaskExecutionId { get; set; }
    public DateTime Timestamp { get; set; }
    public string Level { get; set; } = "Info";
    public string Message { get; set; } = "";
    public string? Details { get; set; }
}
// API Response DTOs
// ============================================

/// <summary>
/// Standard API response envelope.
/// </summary>
public class ApiResponse<T>
{
    /// <summary>
    /// Whether the request was successful.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Response data (if successful).
    /// </summary>
    public T Data { get; set; } = default!;

    /// <summary>
    /// Error message (if unsuccessful).
    /// </summary>
    public string Message { get; set; } = "";

    /// <summary>
    /// Error code for programmatic handling.
    /// </summary>
    public string ErrorCode { get; set; } = "";
}

/// <summary>
/// Pagination parameters for list endpoints.
/// </summary>
public class PaginatedResponse<T>
{
    /// <summary>
    /// List of items.
    /// </summary>
    public List<T> Items { get; set; } = new();

    /// <summary>
    /// Total number of items (ignoring pagination).
    /// </summary>
    public int Total { get; set; }

    /// <summary>
    /// Current page number.
    /// </summary>
    public int Page { get; set; }

    /// <summary>
    /// Page size.
    /// </summary>
    public int PageSize { get; set; }

    /// <summary>
    /// Total number of pages.
    /// </summary>
    public int TotalPages => (Total + PageSize - 1) / PageSize;
}
