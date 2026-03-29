using Microsoft.Data.SqlClient;
using System.Data;
using AScheduler.Api.Dtos;

namespace AScheduler.Api.Services;

/// <summary>
/// Service for managing users in the system.
/// </summary>
public interface IUserService
{
    /// <summary>
    /// Retrieves all users with pagination.
    /// </summary>
    Task<PaginatedResponse<UserDto>> GetAllUsersAsync(int page = 1, int pageSize = 10);

    /// <summary>
    /// Retrieves a user by ID.
    /// </summary>
    Task<UserDto?> GetUserByIdAsync(int userId);

    /// <summary>
    /// Creates a new user.
    /// </summary>
    Task<UserDto?> CreateUserAsync(CreateUserRequest request);

    /// <summary>
    /// Updates user information.
    /// </summary>
    Task<bool> UpdateUserAsync(int userId, UpdateUserRequest request);

    /// <summary>
    /// Deletes a user (soft delete).
    /// </summary>
    Task<bool> DeleteUserAsync(int userId);

    /// <summary>
    /// Changes the password of the current user.
    /// </summary>
    Task ChangeOwnPasswordAsync(int userId, string currentPassword, string newPassword);
}

/// <summary>
/// Implementation of user service using SQL Server.
/// </summary>
public class UserService : IUserService
{
    private readonly IConfiguration _configuration;
    private readonly IAuthenticationService _authenticationService;
    private readonly ILogger<UserService> _logger;

    /// <summary>
    /// Initializes a new instance of the UserService class.
    /// </summary>
    public UserService(
        IConfiguration configuration,
        IAuthenticationService authenticationService,
        ILogger<UserService> logger)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(authenticationService);
        ArgumentNullException.ThrowIfNull(logger);

        _configuration = configuration;
        _authenticationService = authenticationService;
        _logger = logger;
    }

    /// <summary>
    /// Retrieves all users with pagination.
    /// </summary>
    public async Task<PaginatedResponse<UserDto>> GetAllUsersAsync(int page = 1, int pageSize = 10)
    {
        page = Math.Max(1, page);
        pageSize = Math.Max(1, Math.Min(pageSize, 100));

        try
        {
            using var connection = new SqlConnection(_configuration.GetConnectionString("Default"));
            await connection.OpenAsync();

            // Get total count
            var countQuery = "SELECT COUNT(*) FROM Users WHERE IsActive = 1";
            using var countCommand = new SqlCommand(countQuery, connection);
            var countResult = await countCommand.ExecuteScalarAsync();
            var total = countResult is DBNull ? 0 : (int)countResult;

            // Get paginated results
            var query = @"
                SELECT u.UserId, u.Username, u.Email, u.RoleId, r.RoleName, u.IsActive, u.CreatedAt
                FROM Users u
                INNER JOIN Roles r ON u.RoleId = r.RoleId
                WHERE u.IsActive = 1
                ORDER BY u.CreatedAt DESC
                OFFSET (@Page - 1) * @PageSize ROWS
                FETCH NEXT @PageSize ROWS ONLY";

            using var command = new SqlCommand(query, connection);
            command.Parameters.AddWithValue("@Page", page);
            command.Parameters.AddWithValue("@PageSize", pageSize);

            var users = new List<UserDto>();
            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                users.Add(new UserDto
                {
                    UserId = reader.GetInt32(0),
                    Username = reader.GetString(1),
                    Email = reader.IsDBNull(2) ? "" : reader.GetString(2),
                    RoleId = reader.GetInt32(3),
                    RoleName = reader.GetString(4),
                    IsActive = reader.GetBoolean(5),
                    CreatedAt = reader.GetDateTime(6)
                });
            }

            return new PaginatedResponse<UserDto>
            {
                Items = users,
                Total = total,
                Page = page,
                PageSize = pageSize
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving users.");
            throw;
        }
    }

    /// <summary>
    /// Retrieves a user by ID.
    /// </summary>
    public async Task<UserDto?> GetUserByIdAsync(int userId)
    {
        try
        {
            using var connection = new SqlConnection(_configuration.GetConnectionString("Default"));
            await connection.OpenAsync();

            var query = @"
                SELECT u.UserId, u.Username, u.Email, u.RoleId, r.RoleName, u.IsActive, u.CreatedAt
                FROM Users u
                INNER JOIN Roles r ON u.RoleId = r.RoleId
                WHERE u.UserId = @UserId AND u.IsActive = 1";

            using var command = new SqlCommand(query, connection);
            command.Parameters.AddWithValue("@UserId", userId);

            using var reader = await command.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
                return null;

            return new UserDto
            {
                UserId = reader.GetInt32(0),
                Username = reader.GetString(1),
                Email = reader.IsDBNull(2) ? "" : reader.GetString(2),
                RoleId = reader.GetInt32(3),
                RoleName = reader.GetString(4),
                IsActive = reader.GetBoolean(5),
                CreatedAt = reader.GetDateTime(6)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving user {UserId}.", userId);
            throw;
        }
    }

    /// <summary>
    /// Creates a new user.
    /// </summary>
    public async Task<UserDto?> CreateUserAsync(CreateUserRequest request)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Username);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Email);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Password);

        if (request.RoleId < 1 || request.RoleId > 3)
            throw new ArgumentException("Invalid RoleId");

        try
        {
            var passwordHash = _authenticationService.HashPassword(request.Password);

            using var connection = new SqlConnection(_configuration.GetConnectionString("Default"));
            await connection.OpenAsync();

            var query = @"
                INSERT INTO Users (Username, Email, PasswordHash, RoleId, IsActive, CreatedAt)
                VALUES (@Username, @Email, @PasswordHash, @RoleId, 1, GETDATE());
                
                SELECT CAST(SCOPE_IDENTITY() as int);";

            using var command = new SqlCommand(query, connection);
            command.Parameters.AddWithValue("@Username", request.Username);
            command.Parameters.AddWithValue("@Email", request.Email);
            command.Parameters.AddWithValue("@PasswordHash", passwordHash);
            command.Parameters.AddWithValue("@RoleId", request.RoleId);

            var userId = (int?)await command.ExecuteScalarAsync();
            if (userId == null)
                throw new InvalidOperationException("Failed to create user");

            _logger.LogInformation("User {Username} created successfully with ID {UserId}.", request.Username, userId);

            return await GetUserByIdAsync(userId.Value);
        }
        catch (SqlException ex) when (ex.Number == 2627)
        {
            _logger.LogWarning("Attempted to create duplicate user: {Username}.", request.Username);
            throw new InvalidOperationException($"Username '{request.Username}' already exists.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating user {Username}.", request.Username);
            throw;
        }
    }

    /// <summary>
    /// Updates user information.
    /// </summary>
    public async Task<bool> UpdateUserAsync(int userId, UpdateUserRequest request)
    {
        try
        {
            using var connection = new SqlConnection(_configuration.GetConnectionString("Default"));
            await connection.OpenAsync();

            var query = @"
                UPDATE Users
                SET Email = @Email, RoleId = @RoleId, IsActive = @IsActive, UpdatedAt = GETDATE()
                WHERE UserId = @UserId";

            using var command = new SqlCommand(query, connection);
            command.Parameters.AddWithValue("@UserId", userId);
            command.Parameters.AddWithValue("@Email", request.Email ?? "");
            command.Parameters.AddWithValue("@RoleId", request.RoleId);
            command.Parameters.AddWithValue("@IsActive", request.IsActive);

            var rowsAffected = await command.ExecuteNonQueryAsync();
            
            if (rowsAffected > 0)
                _logger.LogInformation("User {UserId} updated successfully.", userId);

            return rowsAffected > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating user {UserId}.", userId);
            throw;
        }
    }

    /// <summary>
    /// Deletes a user (soft delete by setting IsActive to 0).
    /// </summary>
    public async Task<bool> DeleteUserAsync(int userId)
    {
        try
        {
            using var connection = new SqlConnection(_configuration.GetConnectionString("Default"));
            await connection.OpenAsync();

            var query = @"
                UPDATE Users
                SET IsActive = 0, UpdatedAt = GETDATE()
                WHERE UserId = @UserId";

            using var command = new SqlCommand(query, connection);
            command.Parameters.AddWithValue("@UserId", userId);

            var rowsAffected = await command.ExecuteNonQueryAsync();
            
            if (rowsAffected > 0)
                _logger.LogInformation("User {UserId} deleted (soft delete).", userId);

            return rowsAffected > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting user {UserId}.", userId);
            throw;
        }
    }

    public async Task ChangeOwnPasswordAsync(int userId, string currentPassword, string newPassword)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(currentPassword);
        ArgumentException.ThrowIfNullOrWhiteSpace(newPassword);

        if (newPassword.Length < 8 || !newPassword.Any(char.IsLetter) || !newPassword.Any(char.IsDigit))
            throw new InvalidOperationException("New password must be at least 8 characters and include at least one letter and one number.");

        using var connection = new SqlConnection(_configuration.GetConnectionString("Default"));
        await connection.OpenAsync();

        const string selectSql = @"
            SELECT PasswordHash, IsActive
            FROM Users
            WHERE UserId = @UserId";

        using var selectCommand = new SqlCommand(selectSql, connection);
        selectCommand.Parameters.AddWithValue("@UserId", userId);

        using var reader = await selectCommand.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
            throw new InvalidOperationException("User not found.");

        var passwordHash = reader.GetString(0);
        var isActive = reader.GetBoolean(1);
        await reader.CloseAsync();

        if (!isActive)
            throw new InvalidOperationException("Inactive users cannot change password.");

        if (!_authenticationService.VerifyPassword(currentPassword, passwordHash))
            throw new InvalidOperationException("Current password is incorrect.");

        var newPasswordHash = _authenticationService.HashPassword(newPassword);
        const string updateSql = @"
            UPDATE Users
            SET PasswordHash = @PasswordHash, UpdatedAt = GETDATE()
            WHERE UserId = @UserId";

        using var updateCommand = new SqlCommand(updateSql, connection);
        updateCommand.Parameters.AddWithValue("@PasswordHash", newPasswordHash);
        updateCommand.Parameters.AddWithValue("@UserId", userId);

        var rowsAffected = await updateCommand.ExecuteNonQueryAsync();
        if (rowsAffected == 0)
            throw new InvalidOperationException("Password could not be updated.");

        _logger.LogInformation("User {UserId} changed password successfully.", userId);
    }
}
