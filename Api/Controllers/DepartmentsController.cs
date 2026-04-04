using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using AScheduler.Api.Dtos;
using AScheduler.Api.Services;
using AScheduler.Data;
using AScheduler.Domain;

namespace AScheduler.Api.Controllers;

/// <summary>
/// Controller for department (organizational unit) management.
/// Departments provide governance boundaries and retry policy controls.
/// Admins only may create, update, or delete departments.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
public class DepartmentsController : ControllerBase
{
    private readonly IDepartmentRepository _departmentRepository;
    private readonly IAuditLogService _auditLog;
    private readonly ILogger<DepartmentsController> _logger;

    public DepartmentsController(
        IDepartmentRepository departmentRepository,
        IAuditLogService auditLog,
        ILogger<DepartmentsController> logger)
    {
        ArgumentNullException.ThrowIfNull(departmentRepository);
        ArgumentNullException.ThrowIfNull(auditLog);
        ArgumentNullException.ThrowIfNull(logger);
        _departmentRepository = departmentRepository;
        _auditLog = auditLog;
        _logger = logger;
    }

    /// <summary>
    /// Get all departments in the system.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        try
        {
            var departments = await _departmentRepository.GetAllAsync();
            var dtos = departments.Select(MapToDepartmentDto).ToList();
            return Ok(new ApiResponse<List<DepartmentDto>>
            {
                Success = true,
                Data = dtos
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving departments");
            return StatusCode(500, new ApiResponse<object>
            {
                Success = false,
                Message = "Error retrieving departments.",
                ErrorCode = "RETRIEVAL_ERROR"
            });
        }
    }

    /// <summary>
    /// Get a specific department by ID.
    /// </summary>
    [HttpGet("{departmentId}")]
    public async Task<IActionResult> GetById(int departmentId)
    {
        try
        {
            var department = await _departmentRepository.GetByIdAsync(departmentId);
            if (department == null)
                return NotFound(new ApiResponse<object>
                {
                    Success = false,
                    Message = "Department not found.",
                    ErrorCode = "DEPARTMENT_NOT_FOUND"
                });

            var dto = MapToDepartmentDto(department);
            return Ok(new ApiResponse<DepartmentDto>
            {
                Success = true,
                Data = dto
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving department {DepartmentId}", departmentId);
            return StatusCode(500, new ApiResponse<object>
            {
                Success = false,
                Message = "Error retrieving department.",
                ErrorCode = "RETRIEVAL_ERROR"
            });
        }
    }

    /// <summary>
    /// Create a new department.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateDepartmentRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(new ApiResponse<object>
            {
                Success = false,
                Message = "Department name is required.",
                ErrorCode = "MISSING_FIELDS"
            });

        if (request.LogRetentionDays <= 0)
            return BadRequest(new ApiResponse<object>
            {
                Success = false,
                Message = "Log retention days must be greater than zero.",
                ErrorCode = "INVALID_RETENTION"
            });

        if (!TryValidateContactEmail(request.ContactEmail, out var contactEmailError))
            return BadRequest(new ApiResponse<object>
            {
                Success = false,
                Message = contactEmailError,
                ErrorCode = "INVALID_CONTACT_EMAIL"
            });

        if (request.RetryPolicy < 0 || request.RetryPolicy > 2)
            return BadRequest(new ApiResponse<object>
            {
                Success = false,
                Message = "Invalid retry policy. Must be 0 (RequireApproval), 1 (Auto), or 2 (ManualOnly).",
                ErrorCode = "INVALID_RETRY_POLICY"
            });

        try
        {
            // Check if department name already exists
            var existing = await _departmentRepository.GetByNameAsync(request.Name);
            if (existing != null)
                return Conflict(new ApiResponse<object>
                {
                    Success = false,
                    Message = "A department with this name already exists.",
                    ErrorCode = "DUPLICATE_NAME"
                });

            var departmentId = await _departmentRepository.CreateAsync(
                request.Name,
                request.Description,
                request.ContactEmail,
                request.RetryPolicy,
                request.LogRetentionDays);

            var userId = GetCurrentUserId();
            if (userId.HasValue)
                await _auditLog.LogAsync(userId.Value, "Departments", departmentId, "Create", newValues: request.Name);

            var createdDepartment = await _departmentRepository.GetByIdAsync(departmentId);
            var dto = MapToDepartmentDto(createdDepartment!);

            return CreatedAtAction(nameof(GetById), new { departmentId }, new ApiResponse<DepartmentDto>
            {
                Success = true,
                Data = dto
            });
        }
        catch (SqlException ex) when (ex.Number is 2601 or 2627)
        {
            _logger.LogWarning(ex, "Duplicate department name: {DepartmentName}", request.Name);
            return Conflict(new ApiResponse<object>
            {
                Success = false,
                Message = "A department with this name already exists.",
                ErrorCode = "DUPLICATE_NAME"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating department {DepartmentName}", request.Name);
            return StatusCode(500, new ApiResponse<object>
            {
                Success = false,
                Message = "Error creating department.",
                ErrorCode = "CREATION_ERROR"
            });
        }
    }

    /// <summary>
    /// Update an existing department.
    /// </summary>
    [HttpPut("{departmentId}")]
    public async Task<IActionResult> Update(int departmentId, [FromBody] UpdateDepartmentRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(new ApiResponse<object>
            {
                Success = false,
                Message = "Department name is required.",
                ErrorCode = "MISSING_FIELDS"
            });

        if (request.LogRetentionDays <= 0)
            return BadRequest(new ApiResponse<object>
            {
                Success = false,
                Message = "Log retention days must be greater than zero.",
                ErrorCode = "INVALID_RETENTION"
            });

        if (!TryValidateContactEmail(request.ContactEmail, out var contactEmailError))
            return BadRequest(new ApiResponse<object>
            {
                Success = false,
                Message = contactEmailError,
                ErrorCode = "INVALID_CONTACT_EMAIL"
            });

        if (request.RetryPolicy < 0 || request.RetryPolicy > 2)
            return BadRequest(new ApiResponse<object>
            {
                Success = false,
                Message = "Invalid retry policy. Must be 0 (RequireApproval), 1 (Auto), or 2 (ManualOnly).",
                ErrorCode = "INVALID_RETRY_POLICY"
            });

        try
        {
            var existing = await _departmentRepository.GetByIdAsync(departmentId);
            if (existing == null)
                return NotFound(new ApiResponse<object>
                {
                    Success = false,
                    Message = "Department not found.",
                    ErrorCode = "DEPARTMENT_NOT_FOUND"
                });

            // Prevent edition of "Default" department
            if (existing.Name == "Default")
                return BadRequest(new ApiResponse<object>
                {
                    Success = false,
                    Message = "The 'Default' department cannot be edited.",
                    ErrorCode = "CANNOT_EDIT_DEFAULT"
                });

            // Check if new name conflicts with another department
            if (existing.Name != request.Name)
            {
                var conflicting = await _departmentRepository.GetByNameAsync(request.Name);
                if (conflicting != null)
                    return Conflict(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "A department with this name already exists.",
                        ErrorCode = "DUPLICATE_NAME"
                    });
            }

            var updated = await _departmentRepository.UpdateAsync(
                departmentId,
                request.Name,
                request.Description,
                request.ContactEmail,
                request.RetryPolicy,
                request.LogRetentionDays);

            if (!updated)
                return NotFound(new ApiResponse<object>
                {
                    Success = false,
                    Message = "Department not found.",
                    ErrorCode = "DEPARTMENT_NOT_FOUND"
                });

            var userId = GetCurrentUserId();
            if (userId.HasValue)
                await _auditLog.LogAsync(userId.Value, "Departments", departmentId, "Update", newValues: request.Name);

            var updatedDepartment = await _departmentRepository.GetByIdAsync(departmentId);
            var dto = MapToDepartmentDto(updatedDepartment!);

            return Ok(new ApiResponse<DepartmentDto>
            {
                Success = true,
                Data = dto
            });
        }
        catch (SqlException ex) when (ex.Number is 2601 or 2627)
        {
            _logger.LogWarning(ex, "Duplicate department name: {DepartmentName}", request.Name);
            return Conflict(new ApiResponse<object>
            {
                Success = false,
                Message = "A department with this name already exists.",
                ErrorCode = "DUPLICATE_NAME"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating department {DepartmentId}", departmentId);
            return StatusCode(500, new ApiResponse<object>
            {
                Success = false,
                Message = "Error updating department.",
                ErrorCode = "UPDATE_ERROR"
            });
        }
    }

    /// <summary>
    /// Delete a department.
    /// Note: Cannot delete a department that has boxes or users assigned.
    /// </summary>
    [HttpDelete("{departmentId}")]
    public async Task<IActionResult> Delete(int departmentId)
    {
        try
        {
            var existing = await _departmentRepository.GetByIdAsync(departmentId);
            if (existing == null)
                return NotFound(new ApiResponse<object>
                {
                    Success = false,
                    Message = "Department not found.",
                    ErrorCode = "DEPARTMENT_NOT_FOUND"
                });

            // Prevent deletion of "Default" department
            if (existing.Name == "Default")
                return BadRequest(new ApiResponse<object>
                {
                    Success = false,
                    Message = "The 'Default' department cannot be deleted.",
                    ErrorCode = "CANNOT_DELETE_DEFAULT"
                });

            // Check if department has boxes or users
            var boxes = await _departmentRepository.GetDepartmentBoxesAsync(departmentId);
            var users = await _departmentRepository.GetDepartmentUsersAsync(departmentId);

            if (boxes.Count > 0 || users.Count > 0)
                return BadRequest(new ApiResponse<object>
                {
                    Success = false,
                    Message = $"Cannot delete department with {boxes.Count} boxes and {users.Count} users. Reassign them first.",
                    ErrorCode = "DEPARTMENT_NOT_EMPTY"
                });

            var deleted = await _departmentRepository.DeleteAsync(departmentId);
            if (!deleted)
                return NotFound(new ApiResponse<object>
                {
                    Success = false,
                    Message = "Department not found.",
                    ErrorCode = "DEPARTMENT_NOT_FOUND"
                });

            var userId = GetCurrentUserId();
            if (userId.HasValue)
                await _auditLog.LogAsync(userId.Value, "Departments", departmentId, "Delete", newValues: existing.Name);

            return Ok(new ApiResponse<object>
            {
                Success = true,
                Message = "Department deleted successfully."
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting department {DepartmentId}", departmentId);
            return StatusCode(500, new ApiResponse<object>
            {
                Success = false,
                Message = "Error deleting department.",
                ErrorCode = "DELETION_ERROR"
            });
        }
    }

    /// <summary>
    /// Get the retry policy for a department.
    /// </summary>
    [HttpGet("{departmentId}/retry-policy")]
    public async Task<IActionResult> GetRetryPolicy(int departmentId)
    {
        try
        {
            var policy = await _departmentRepository.GetRetryPolicyAsync(departmentId);
            if (policy == null)
                return NotFound(new ApiResponse<object>
                {
                    Success = false,
                    Message = "Department not found.",
                    ErrorCode = "DEPARTMENT_NOT_FOUND"
                });

            var policyName = policy switch
            {
                0 => "RequireApproval",
                1 => "Auto",
                2 => "ManualOnly",
                _ => "Unknown"
            };

            return Ok(new ApiResponse<object>
            {
                Success = true,
                Data = new { Policy = policyName, PolicyValue = policy }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving retry policy for department {DepartmentId}", departmentId);
            return StatusCode(500, new ApiResponse<object>
            {
                Success = false,
                Message = "Error retrieving retry policy.",
                ErrorCode = "RETRIEVAL_ERROR"
            });
        }
    }

    private int? GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
        return userIdClaim != null && int.TryParse(userIdClaim.Value, out var userId) ? userId : null;
    }

    private static DepartmentDto MapToDepartmentDto(Department department)
    {
        return new DepartmentDto
        {
            DepartmentId = department.DepartmentId,
            Name = department.Name,
            Description = department.Description,
            ContactEmail = department.ContactEmail,
            RetryPolicy = (int)department.RetryPolicy,
            LogRetentionDays = department.LogRetentionDays,
            CreatedAt = UtcDateTimeMapper.EnsureUtc(department.CreatedAt),
            UpdatedAt = UtcDateTimeMapper.EnsureUtc(department.UpdatedAt ?? DateTime.UtcNow)
        };
    }

    private static bool TryValidateContactEmail(string? email, out string error)
    {
        error = "";
        if (string.IsNullOrWhiteSpace(email))
        {
            error = "Department contact email is required.";
            return false;
        }

        var normalized = email.Trim();
        if (normalized.Length > 255)
        {
            error = "Department contact email must not exceed 255 characters.";
            return false;
        }

        try
        {
            var addr = new System.Net.Mail.MailAddress(normalized);
            if (addr.Address != normalized)
            {
                error = "Department contact email format is invalid.";
                return false;
            }

            return true;
        }
        catch
        {
            error = "Department contact email format is invalid.";
            return false;
        }
    }
}
