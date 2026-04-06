using Microsoft.AspNetCore.Mvc;
using CHRONIQ.Api.Dtos;
using CHRONIQ.Api.Services;

/// <summary>
/// Controller for authentication operations.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthenticationService _authenticationService;
    private readonly ILogger<AuthController> _logger;

    /// <summary>
    /// Initializes a new instance of the AuthController class.
    /// </summary>
    public AuthController(
        IAuthenticationService authenticationService,
        ILogger<AuthController> logger)
    {
        ArgumentNullException.ThrowIfNull(authenticationService);
        ArgumentNullException.ThrowIfNull(logger);

        _authenticationService = authenticationService;
        _logger = logger;
    }

    /// <summary>
    /// Authenticates a user and returns a JWT token.
    /// </summary>
    /// <param name="request">Login credentials.</param>
    /// <returns>JWT token and user information if successful.</returns>
    [HttpPost("login")]
    [ProducesResponseType(typeof(ApiResponse<LoginResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
        {
            _logger.LogWarning("Login attempt with missing credentials.");
            return BadRequest(new ApiResponse<object>
            {
                Success = false,
                Message = "Username and password are required.",
                ErrorCode = "MISSING_CREDENTIALS"
            });
        }

        var (user, token) = await _authenticationService.AuthenticateAsync(
            request.Username,
            request.Password);

        if (user == null || token == null)
        {
            return Unauthorized(new ApiResponse<object>
            {
                Success = false,
                Message = "Invalid username or password.",
                ErrorCode = "INVALID_CREDENTIALS"
            });
        }

        _logger.LogInformation("User {Username} logged in successfully.", request.Username);

        return Ok(new ApiResponse<LoginResponse>
        {
            Success = true,
            Data = new LoginResponse
            {
                AccessToken = token,
                User = user
            },
            Message = "Login successful."
        });
    }
}
