using System.Diagnostics;
using System.Net.Mail;
using System.Security.Claims;
using AScheduler.Api.Dtos;
using AScheduler.Api.Services;
using AScheduler.Data;
using AScheduler.Domain;
using AScheduler.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AScheduler.Api.Controllers;

/// <summary>
/// Admin endpoints for SMTP notification settings and live send tests.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
public class NotificationSettingsController : ControllerBase
{
    private readonly INotificationSettingsRepository _settingsRepository;
    private readonly ISmtpMailSender _smtpMailSender;
    private readonly IAuditLogService _auditLogService;
    private readonly ILogger<NotificationSettingsController> _logger;

    public NotificationSettingsController(
        INotificationSettingsRepository settingsRepository,
        ISmtpMailSender smtpMailSender,
        IAuditLogService auditLogService,
        ILogger<NotificationSettingsController> logger)
    {
        ArgumentNullException.ThrowIfNull(settingsRepository);
        ArgumentNullException.ThrowIfNull(smtpMailSender);
        ArgumentNullException.ThrowIfNull(auditLogService);
        ArgumentNullException.ThrowIfNull(logger);

        _settingsRepository = settingsRepository;
        _smtpMailSender = smtpMailSender;
        _auditLogService = auditLogService;
        _logger = logger;
    }

    [HttpGet("smtp")]
    public async Task<IActionResult> GetSmtpSettings()
    {
        var settings = await _settingsRepository.GetEffectiveSmtpSettingsAsync();
        return Ok(new ApiResponse<SmtpNotificationSettingsDto>
        {
            Success = true,
            Data = ToDto(settings)
        });
    }

    [HttpPut("smtp")]
    public async Task<IActionResult> UpdateSmtpSettings([FromBody] UpdateSmtpNotificationSettingsRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!ValidateSettingsRequest(request, out var validationError))
        {
            return BadRequest(new ApiResponse<object>
            {
                Success = false,
                Message = validationError,
                ErrorCode = "INVALID_SMTP_SETTINGS"
            });
        }

        var settings = new NotificationSmtpSettings
        {
            Enabled = request.Enabled,
            Host = request.Host.Trim(),
            Port = request.Port,
            Username = request.Username.Trim(),
            Password = request.Password?.Trim() ?? string.Empty,
            FromAddress = request.FromAddress.Trim(),
            FromDisplayName = request.FromDisplayName.Trim(),
            EnableSsl = request.EnableSsl
        };

        var overwritePassword = !string.IsNullOrWhiteSpace(request.Password);
        var userId = GetCurrentUserId();
        var saved = await _settingsRepository.UpsertSmtpSettingsAsync(settings, userId, overwritePassword);

        if (userId.HasValue)
        {
            await _auditLogService.LogAsync(
                userId.Value,
                "NotificationSmtpSettings",
                1,
                "Update",
                newValues: $"Enabled={saved.Enabled}; Host={saved.Host}; Port={saved.Port}; Username={saved.Username}; EnableSsl={saved.EnableSsl}");
        }

        return Ok(new ApiResponse<SmtpNotificationSettingsDto>
        {
            Success = true,
            Data = ToDto(saved),
            Message = "SMTP settings updated successfully."
        });
    }

    [HttpPost("smtp/test")]
    public async Task<IActionResult> SendSmtpTest([FromBody] TestSmtpNotificationRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.TestRecipientEmail) || !IsValidEmail(request.TestRecipientEmail))
        {
            return BadRequest(new ApiResponse<object>
            {
                Success = false,
                Message = "A valid testRecipientEmail is required.",
                ErrorCode = "INVALID_TEST_RECIPIENT"
            });
        }

        var settings = await _settingsRepository.GetEffectiveSmtpSettingsAsync();
        if (!settings.Enabled)
        {
            return BadRequest(new ApiResponse<object>
            {
                Success = false,
                Message = "SMTP notifications are currently disabled.",
                ErrorCode = "SMTP_DISABLED"
            });
        }

        var stopwatch = Stopwatch.StartNew();
        try
        {
            var subject = "[AScheduler] SMTP Test Message";
            var body = $"SMTP configuration test completed successfully at {DateTime.UtcNow:O} UTC.";

            await _smtpMailSender.SendAsync(
                settings,
                request.TestRecipientEmail.Trim(),
                subject,
                body,
                cancellationToken);

            stopwatch.Stop();

            var userId = GetCurrentUserId();
            if (userId.HasValue)
            {
                await _auditLogService.LogAsync(
                    userId.Value,
                    "NotificationSmtpSettings",
                    1,
                    "Test",
                    newValues: $"Recipient={request.TestRecipientEmail.Trim()}; Result=Success");
            }

            return Ok(new ApiResponse<SmtpTestResultDto>
            {
                Success = true,
                Data = new SmtpTestResultDto
                {
                    Success = true,
                    Message = "Test email sent successfully.",
                    DurationMs = stopwatch.ElapsedMilliseconds
                }
            });
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(
                ex,
                "SMTP test send failed. Recipient={Recipient}, DurationMs={DurationMs}",
                MaskEmail(request.TestRecipientEmail),
                stopwatch.ElapsedMilliseconds);

            var userId = GetCurrentUserId();
            if (userId.HasValue)
            {
                await _auditLogService.LogAsync(
                    userId.Value,
                    "NotificationSmtpSettings",
                    1,
                    "Test",
                    newValues: $"Recipient={request.TestRecipientEmail.Trim()}; Result=Failed; Message={ex.Message}");
            }

            return BadRequest(new ApiResponse<SmtpTestResultDto>
            {
                Success = false,
                Message = ex.Message,
                ErrorCode = "SMTP_TEST_FAILED",
                Data = new SmtpTestResultDto
                {
                    Success = false,
                    Message = ex.Message,
                    DurationMs = stopwatch.ElapsedMilliseconds
                }
            });
        }
    }

    private static SmtpNotificationSettingsDto ToDto(NotificationSmtpSettings settings)
    {
        return new SmtpNotificationSettingsDto
        {
            Enabled = settings.Enabled,
            Host = settings.Host,
            Port = settings.Port,
            Username = settings.Username,
            HasPassword = !string.IsNullOrWhiteSpace(settings.Password),
            FromAddress = settings.FromAddress,
            FromDisplayName = settings.FromDisplayName,
            EnableSsl = settings.EnableSsl
        };
    }

    private static bool ValidateSettingsRequest(UpdateSmtpNotificationSettingsRequest request, out string validationError)
    {
        if (request.Port is < 1 or > 65535)
        {
            validationError = "Port must be between 1 and 65535.";
            return false;
        }

        if (request.Enabled)
        {
            if (string.IsNullOrWhiteSpace(request.Host))
            {
                validationError = "Host is required when SMTP notifications are enabled.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(request.FromAddress) || !IsValidEmail(request.FromAddress))
            {
                validationError = "A valid FromAddress is required when SMTP notifications are enabled.";
                return false;
            }
        }

        validationError = string.Empty;
        return true;
    }

    private int? GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(userIdClaim, out var userId) ? userId : null;
    }

    private static bool IsValidEmail(string email)
    {
        try
        {
            _ = new MailAddress(email);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string MaskEmail(string email)
    {
        var atIndex = email.IndexOf('@');
        if (atIndex <= 1 || atIndex == email.Length - 1)
        {
            return "***";
        }

        var local = email[..atIndex];
        var domain = email[(atIndex + 1)..];
        var visibleLocal = local.Length <= 2 ? local[0].ToString() : local[..2];
        return $"{visibleLocal}***@{domain}";
    }
}
