using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CHRONIQ.Api.Dtos;
using CHRONIQ.Api.Services;
using System.Security.Claims;

namespace CHRONIQ.Api.Controllers;

/// <summary>
/// Controller for user management operations.
/// Requires authentication and admin role for certain operations.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly ILogger<UsersController> _logger;

    /// <summary>
    /// Initializes a new instance of the UsersController class.
    /// </summary>
    public UsersController(
        IUserService userService,
        ILogger<UsersController> logger)
    {
        ArgumentNullException.ThrowIfNull(userService);
        ArgumentNullException.ThrowIfNull(logger);

        _userService = userService;
        _logger = logger;
    }

    /// <summary>
    /// Retrieves all users with pagination.
    /// </summary>
    /// <param name="page">Page number (1-based).</param>
    /// <param name="pageSize">Number of items per page.</param>
    /// <returns>Paginated list of users.</returns>
    [HttpGet]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(ApiResponse<PaginatedResponse<UserDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetAllUsers([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
    {
        var response = await _userService.GetAllUsersAsync(page, pageSize);
        
        _logger.LogInformation("Retrieved {Count} users (page {Page}).", response.Items.Count, page);

        return Ok(new ApiResponse<PaginatedResponse<UserDto>>
        {
            Success = true,
            Data = response,
            Message = "Users retrieved successfully."
        });
    }

    /// <summary>
    /// Retrieves a specific user by ID.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <returns>User details if found.</returns>
    [HttpGet("{userId}")]
    [ProducesResponseType(typeof(ApiResponse<UserDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetUserById(int userId)
    {
        var user = await _userService.GetUserByIdAsync(userId);

        if (user == null)
        {
            _logger.LogWarning("User {UserId} not found.", userId);
            return NotFound(new ApiResponse<object>
            {
                Success = false,
                Message = "User not found.",
                ErrorCode = "USER_NOT_FOUND"
            });
        }

        return Ok(new ApiResponse<UserDto>
        {
            Success = true,
            Data = user,
            Message = "User retrieved successfully."
        });
    }

    /// <summary>
    /// Creates a new user.
    /// </summary>
    /// <param name="request">User creation request.</param>
    /// <returns>Created user information.</returns>
    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(ApiResponse<UserDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreateUser([FromBody] CreateUserRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(new ApiResponse<object>
            {
                Success = false,
                Message = "Invalid request.",
                ErrorCode = "INVALID_REQUEST"
            });
        }

        try
        {
            var user = await _userService.CreateUserAsync(request);

            if (user == null)
            {
                return BadRequest(new ApiResponse<object>
                {
                    Success = false,
                    Message = "Failed to create user.",
                    ErrorCode = "CREATION_FAILED"
                });
            }

            _logger.LogInformation("User {Username} created successfully.", request.Username);

            return CreatedAtAction(nameof(GetUserById), new { userId = user.UserId }, 
                new ApiResponse<UserDto>
                {
                    Success = true,
                    Data = user,
                    Message = "User created successfully."
                });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("Failed to create user: {Message}", ex.Message);
            return BadRequest(new ApiResponse<object>
            {
                Success = false,
                Message = ex.Message,
                ErrorCode = "USER_EXISTS"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating user {Username}.", request.Username);
            return BadRequest(new ApiResponse<object>
            {
                Success = false,
                Message = "Error creating user.",
                ErrorCode = "CREATION_ERROR"
            });
        }
    }

    /// <summary>
    /// Updates an existing user.
    /// </summary>
    /// <param name="userId">The user ID to update.</param>
    /// <param name="request">Update request.</param>
    /// <returns>Success status.</returns>
    [HttpPut("{userId}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(ApiResponse<UserDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> UpdateUser(int userId, [FromBody] UpdateUserRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(new ApiResponse<object>
            {
                Success = false,
                Message = "Invalid request.",
                ErrorCode = "INVALID_REQUEST"
            });
        }

        try
        {
            var updated = await _userService.UpdateUserAsync(userId, request);

            if (!updated)
            {
                return NotFound(new ApiResponse<object>
                {
                    Success = false,
                    Message = "User not found.",
                    ErrorCode = "USER_NOT_FOUND"
                });
            }

            var user = await _userService.GetUserByIdAsync(userId);

            _logger.LogInformation("User {UserId} updated successfully.", userId);

            return Ok(new ApiResponse<UserDto>
            {
                Success = true,
                Data = user!,
                Message = "User updated successfully."
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating user {UserId}.", userId);
            return BadRequest(new ApiResponse<object>
            {
                Success = false,
                Message = "Error updating user.",
                ErrorCode = "UPDATE_ERROR"
            });
        }
    }

    /// <summary>
    /// Deletes a user (soft delete).
    /// </summary>
    /// <param name="userId">The user ID to delete.</param>
    /// <returns>Success status.</returns>
    [HttpDelete("{userId}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> DeleteUser(int userId)
    {
        try
        {
            var deleted = await _userService.DeleteUserAsync(userId);

            if (!deleted)
            {
                return NotFound(new ApiResponse<object>
                {
                    Success = false,
                    Message = "User not found.",
                    ErrorCode = "USER_NOT_FOUND"
                });
            }

            _logger.LogInformation("User {UserId} deleted successfully.", userId);

            return Ok(new ApiResponse<object>
            {
                Success = true,
                Message = "User deleted successfully."
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting user {UserId}.", userId);
            return BadRequest(new ApiResponse<object>
            {
                Success = false,
                Message = "Error deleting user.",
                ErrorCode = "DELETE_ERROR"
            });
        }
    }

    /// <summary>
    /// Changes the password of the authenticated user.
    /// </summary>
    [HttpPost("change-password")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ChangeOwnPassword([FromBody] ChangePasswordRequest request)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue)
        {
            return Unauthorized(new ApiResponse<object>
            {
                Success = false,
                Message = "User context not found.",
                ErrorCode = "UNAUTHORIZED"
            });
        }

        try
        {
            await _userService.ChangeOwnPasswordAsync(userId.Value, request.CurrentPassword, request.NewPassword);
            return Ok(new ApiResponse<object>
            {
                Success = true,
                Message = "Password changed successfully."
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ApiResponse<object>
            {
                Success = false,
                Message = ex.Message,
                ErrorCode = "PASSWORD_CHANGE_FAILED"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error changing password for user {UserId}.", userId.Value);
            return BadRequest(new ApiResponse<object>
            {
                Success = false,
                Message = "Error changing password.",
                ErrorCode = "PASSWORD_CHANGE_ERROR"
            });
        }
    }

    private int? GetCurrentUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return claim != null && int.TryParse(claim, out var userId) ? userId : null;
    }
}
