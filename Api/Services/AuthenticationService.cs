using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using AScheduler.Api.Dtos;

namespace AScheduler.Api.Services;

/// <summary>
/// Service for managing JWT token generation and validation.
/// </summary>
public interface ITokenService
{
    /// <summary>
    /// Generates a JWT token for the authenticated user.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="username">The username.</param>
    /// <param name="roleName">The user's role name.</param>
    /// <param name="departmentId">The user's department ID.</param>
    /// <returns>The JWT token string.</returns>
    string GenerateToken(int userId, string username, string roleName, int? departmentId = null);

    /// <summary>
    /// Validates a JWT token and extracts claims.
    /// </summary>
    /// <param name="token">The token to validate.</param>
    /// <returns>ClaimsPrincipal if valid, null otherwise.</returns>
    ClaimsPrincipal? ValidateToken(string token);
}

/// <summary>
/// Implementation of JWT token service.
/// </summary>
public class JwtTokenService : ITokenService
{
    private readonly string _secretKey;
    private readonly string _issuer;
    private readonly string _audience;
    private readonly int _expirationMinutes;

    /// <summary>
    /// Initializes a new instance of the JwtTokenService class.
    /// </summary>
    /// <param name="configuration">Application configuration containing JWT settings.</param>
    /// <exception cref="ArgumentException">Thrown when required JWT settings are missing.</exception>
    public JwtTokenService(IConfiguration configuration)
    {
        (_secretKey, _) = JwtSecretResolver.Resolve(configuration);
        _issuer = configuration["Jwt:Issuer"] ?? "AScheduler";
        _audience = configuration["Jwt:Audience"] ?? "ASchedulerAPI";
        _expirationMinutes = configuration.GetValue<int>("Jwt:ExpirationMinutes", 480);
    }

    /// <summary>
    /// Generates a JWT token for the authenticated user.
    /// </summary>
    public string GenerateToken(int userId, string username, string roleName, int? departmentId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(username);
        ArgumentException.ThrowIfNullOrWhiteSpace(roleName);

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Name, username),
            new(ClaimTypes.Role, roleName),
            new("sub", username)
        };

        // Add department claim if provided
        if (departmentId.HasValue)
        {
            claims.Add(new Claim("department_id", departmentId.Value.ToString()));
        }

        var token = new JwtSecurityToken(
            issuer: _issuer,
            audience: _audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_expirationMinutes),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <summary>
    /// Validates a JWT token and extracts claims.
    /// </summary>
    public ClaimsPrincipal? ValidateToken(string token)
    {
        try
        {
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secretKey));
            var handler = new JwtSecurityTokenHandler();

            var principal = handler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = key,
                ValidateIssuer = true,
                ValidIssuer = _issuer,
                ValidateAudience = true,
                ValidAudience = _audience,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero
            }, out var validatedToken);

            return principal;
        }
        catch
        {
            return null;
        }
    }
}

/// <summary>
/// Service for managing user authentication and passwords.
/// </summary>
public interface IAuthenticationService
{
    /// <summary>
    /// Authenticates a user by username and password.
    /// </summary>
    /// <param name="username">The username.</param>
    /// <param name="password">The plain text password.</param>
    /// <returns>User information with JWT token if authenticated, null otherwise.</returns>
    Task<(UserDto? User, string? Token)> AuthenticateAsync(string username, string password);

    /// <summary>
    /// Hashes a plain text password.
    /// </summary>
    /// <param name="password">The plain text password to hash.</param>
    /// <returns>The hashed password.</returns>
    string HashPassword(string password);

    /// <summary>
    /// Verifies a plain text password against its hash.
    /// </summary>
    /// <param name="password">The plain text password.</param>
    /// <param name="hash">The password hash to verify against.</param>
    /// <returns>True if password matches hash, false otherwise.</returns>
    bool VerifyPassword(string password, string hash);
}

/// <summary>
/// Implementation of authentication service using SQL Server and JWT.
/// </summary>
public class AuthenticationService : IAuthenticationService
{
    private readonly ITokenService _tokenService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AuthenticationService> _logger;

    /// <summary>
    /// Initializes a new instance of the AuthenticationService class.
    /// </summary>
    public AuthenticationService(
        ITokenService tokenService,
        IConfiguration configuration,
        ILogger<AuthenticationService> logger)
    {
        ArgumentNullException.ThrowIfNull(tokenService);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(logger);

        _tokenService = tokenService;
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// Authenticates a user by username and password.
    /// </summary>
    public async Task<(UserDto? User, string? Token)> AuthenticateAsync(string username, string password)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            _logger.LogWarning("Authentication attempt with empty username or password.");
            return (null, null);
        }

        try
        {
            using var connection = new Microsoft.Data.SqlClient.SqlConnection(
                _configuration.GetConnectionString("Default"));
            
            await connection.OpenAsync();

            var query = @"
                SELECT u.UserId, u.Username, u.Email, u.PasswordHash, u.IsActive, r.RoleName, u.DepartmentId
                FROM Users u
                INNER JOIN Roles r ON u.RoleId = r.RoleId
                WHERE u.Username = @Username";

            using var command = new Microsoft.Data.SqlClient.SqlCommand(query, connection);
            command.Parameters.AddWithValue("@Username", username);

            using var reader = await command.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
            {
                _logger.LogWarning("Authentication failed for username: {Username}. User not found.", username);
                return (null, null);
            }

            var userId = reader.GetInt32(0);
            var dbUsername = reader.GetString(1);
            var email = reader.IsDBNull(2) ? "" : reader.GetString(2);
            var passwordHash = reader.GetString(3);
            var isActive = reader.GetBoolean(4);
            var roleName = reader.GetString(5);
            var departmentId = reader.IsDBNull(6) ? (int?)null : reader.GetInt32(6);

            if (!isActive)
            {
                _logger.LogWarning("Authentication failed. User {Username} is inactive.", username);
                return (null, null);
            }

            if (!VerifyPassword(password, passwordHash))
            {
                _logger.LogWarning("Authentication failed for username: {Username}. Invalid password.", username);
                return (null, null);
            }

            var user = new UserDto
            {
                UserId = userId,
                Username = dbUsername,
                Email = email,
                RoleName = roleName,
                IsActive = isActive,
                DepartmentId = departmentId,
                CreatedAt = DateTime.UtcNow
            };

            var token = _tokenService.GenerateToken(userId, dbUsername, roleName, departmentId);

            _logger.LogInformation("User {Username} authenticated successfully.", username);
            return (user, token);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during authentication for username: {Username}.", username);
            return (null, null);
        }
    }

    /// <summary>
    /// Hashes a plain text password using PBKDF2.
    /// </summary>
    public string HashPassword(string password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password);

        using var pbkdf2 = new System.Security.Cryptography.Rfc2898DeriveBytes(
            password,
            new byte[16],
            10000,
            System.Security.Cryptography.HashAlgorithmName.SHA256);

        var hash = pbkdf2.GetBytes(20);
        var hashWithSalt = new byte[36];
        Array.Copy(pbkdf2.Salt, 0, hashWithSalt, 0, 16);
        Array.Copy(hash, 0, hashWithSalt, 16, 20);

        return Convert.ToBase64String(hashWithSalt);
    }

    /// <summary>
    /// Verifies a plain text password against its hash.
    /// </summary>
    public bool VerifyPassword(string password, string hash)
    {
        if (string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(hash))
            return false;

        try
        {
            var hashBytes = Convert.FromBase64String(hash);
            var salt = new byte[16];
            Array.Copy(hashBytes, 0, salt, 0, 16);

            using var pbkdf2 = new System.Security.Cryptography.Rfc2898DeriveBytes(
                password,
                salt,
                10000,
                System.Security.Cryptography.HashAlgorithmName.SHA256);

            var computedHash = pbkdf2.GetBytes(20);
            for (int i = 0; i < 20; i++)
            {
                if (hashBytes[i + 16] != computedHash[i])
                    return false;
            }

            return true;
        }
        catch
        {
            return false;
        }
    }
}
