using System.Diagnostics;
using CHRONIQ.Data;
using Microsoft.Extensions.Logging;

namespace CHRONIQ.Services
{
    /// <summary>
    /// SMTP-based implementation of task failure notifications.
    /// Sends email alerts to a box's notification email address when tasks fail.
    /// Designed to be fail-safe: exceptions are caught and logged, not propagated.
    /// </summary>
    public class SmtpTaskFailureNotificationService : ITaskFailureNotificationService
    {
        private readonly INotificationSettingsRepository _settingsRepository;
        private readonly ISmtpMailSender _smtpMailSender;
        private readonly ILogger<SmtpTaskFailureNotificationService> _logger;

        public SmtpTaskFailureNotificationService(
            INotificationSettingsRepository settingsRepository,
            ISmtpMailSender smtpMailSender,
            ILogger<SmtpTaskFailureNotificationService> logger)
        {
            ArgumentNullException.ThrowIfNull(settingsRepository);
            ArgumentNullException.ThrowIfNull(smtpMailSender);
            ArgumentNullException.ThrowIfNull(logger);
            _settingsRepository = settingsRepository;
            _smtpMailSender = smtpMailSender;
            _logger = logger;
        }

        public async Task<bool> SendTaskFailureNotificationAsync(
            int boxId,
            int taskId,
            int? boxRunId,
            string taskName,
            string boxName,
            string notificationEmail,
            string failureReason,
            int executionId,
            string triggerSource,
            DateTime? scheduledForUtc,
            string? requestedByUsername,
            CancellationToken cancellationToken)
        {
            var stopwatch = Stopwatch.StartNew();
            var settings = await _settingsRepository.GetEffectiveSmtpSettingsAsync();

            // Validate prerequisites
            if (!settings.Enabled)
            {
                _logger.LogInformation(
                    "SMTP notification skipped because it is disabled. TaskId={TaskId}, ExecutionId={ExecutionId}",
                    taskId,
                    executionId);
                return false;
            }

            if (string.IsNullOrWhiteSpace(settings.Host))
            {
                _logger.LogError(
                    "SMTP notification failed due to invalid configuration (host is empty). TaskId={TaskId}, ExecutionId={ExecutionId}",
                    taskId,
                    executionId);
                return false;
            }

            if (string.IsNullOrWhiteSpace(settings.FromAddress))
            {
                _logger.LogError(
                    "SMTP notification failed due to invalid configuration (from address is empty). TaskId={TaskId}, ExecutionId={ExecutionId}",
                    taskId,
                    executionId);
                return false;
            }

            if (string.IsNullOrWhiteSpace(notificationEmail))
            {
                _logger.LogInformation(
                    "SMTP notification skipped because destination email is empty. BoxId={BoxId}, TaskId={TaskId}, ExecutionId={ExecutionId}",
                    boxId,
                    taskId,
                    executionId);
                return false;
            }

            var recipients = notificationEmail
                .Replace(';', ',')
                .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (recipients.Count == 0)
            {
                _logger.LogWarning(
                    "SMTP notification skipped because no valid recipients were parsed. BoxId={BoxId}, TaskId={TaskId}, ExecutionId={ExecutionId}",
                    boxId,
                    taskId,
                    executionId);
                return false;
            }

            try
            {
                var subject = $"[CHRONIQ Alert] Task Failed: {taskName} in {boxName}";
                var bodyText = BuildFailureEmailBody(
                    boxName, taskName, executionId, boxRunId, failureReason,
                    triggerSource, scheduledForUtc, requestedByUsername);

                var sentCount = 0;
                var failedCount = 0;

                foreach (var recipient in recipients)
                {
                    try
                    {
                        _logger.LogInformation(
                            "Attempting SMTP send. BoxId={BoxId}, TaskId={TaskId}, ExecutionId={ExecutionId}, TriggerSource={TriggerSource}, Host={Host}, Port={Port}, EnableSsl={EnableSsl}, To={ToEmail}",
                            boxId,
                            taskId,
                            executionId,
                            triggerSource,
                            settings.Host,
                            settings.Port,
                            settings.EnableSsl,
                            MaskEmail(recipient));

                        await _smtpMailSender.SendAsync(settings, recipient, subject, bodyText, cancellationToken);
                        sentCount++;
                    }
                    catch (Exception ex)
                    {
                        failedCount++;
                        _logger.LogError(
                            ex,
                            "SMTP send failed for one recipient. BoxId={BoxId}, TaskId={TaskId}, ExecutionId={ExecutionId}, To={ToEmail}",
                            boxId,
                            taskId,
                            executionId,
                            MaskEmail(recipient));
                    }
                }

                stopwatch.Stop();

                if (sentCount > 0 && failedCount == 0)
                {
                    _logger.LogInformation(
                        "SMTP notification sent successfully to all recipients. BoxId={BoxId}, TaskId={TaskId}, TaskName={TaskName}, ExecutionId={ExecutionId}, RecipientCount={RecipientCount}, ElapsedMs={ElapsedMs}",
                        boxId,
                        taskId,
                        taskName,
                        executionId,
                        sentCount,
                        stopwatch.ElapsedMilliseconds);
                    return true;
                }

                _logger.LogWarning(
                    "SMTP notification finished with partial delivery. BoxId={BoxId}, TaskId={TaskId}, ExecutionId={ExecutionId}, SentCount={SentCount}, FailedCount={FailedCount}, ElapsedMs={ElapsedMs}",
                    boxId,
                    taskId,
                    executionId,
                    sentCount,
                    failedCount,
                    stopwatch.ElapsedMilliseconds);

                return sentCount > 0;
            }
            catch (MailKit.Security.AuthenticationException ex)
            {
                stopwatch.Stop();
                _logger.LogError(
                    ex,
                    "SMTP authentication failed. Check credentials or App Password. BoxId={BoxId}, TaskId={TaskId}, ExecutionId={ExecutionId}, To={ToEmail}, ElapsedMs={ElapsedMs}",
                    boxId,
                    taskId,
                    executionId,
                    MaskEmail(notificationEmail),
                    stopwatch.ElapsedMilliseconds);
                return false;
            }
            catch (MailKit.Net.Smtp.SmtpCommandException ex)
            {
                stopwatch.Stop();
                _logger.LogError(
                    ex,
                    "SMTP command error while sending notification. StatusCode={StatusCode}, BoxId={BoxId}, TaskId={TaskId}, ExecutionId={ExecutionId}, To={ToEmail}, ElapsedMs={ElapsedMs}",
                    ex.StatusCode,
                    boxId,
                    taskId,
                    executionId,
                    MaskEmail(notificationEmail),
                    stopwatch.ElapsedMilliseconds);
                return false;
            }
            catch (OperationCanceledException ex) when (cancellationToken.IsCancellationRequested)
            {
                stopwatch.Stop();
                _logger.LogWarning(
                    ex,
                    "SMTP notification send was canceled. BoxId={BoxId}, TaskId={TaskId}, ExecutionId={ExecutionId}, To={ToEmail}, ElapsedMs={ElapsedMs}",
                    boxId,
                    taskId,
                    executionId,
                    MaskEmail(notificationEmail),
                    stopwatch.ElapsedMilliseconds);
                return false;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError(
                    ex,
                    "Unexpected error while sending SMTP notification. BoxId={BoxId}, TaskId={TaskId}, ExecutionId={ExecutionId}, To={ToEmail}, ElapsedMs={ElapsedMs}",
                    boxId,
                    taskId,
                    executionId,
                    MaskEmail(notificationEmail),
                    stopwatch.ElapsedMilliseconds);
                return false;
            }
        }

        private static string MaskEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
                return "<empty>";

            var atIndex = email.IndexOf('@');
            if (atIndex <= 1 || atIndex == email.Length - 1)
                return "***";

            var local = email[..atIndex];
            var domain = email[(atIndex + 1)..];
            var visibleLocal = local.Length <= 2 ? local[0].ToString() : local[..2];
            return $"{visibleLocal}***@{domain}";
        }

        private static string BuildFailureEmailBody(
            string boxName,
            string taskName,
            int executionId,
            int? boxRunId,
            string failureReason,
            string triggerSource,
            DateTime? scheduledForUtc,
            string? requestedByUsername)
        {
            var lines = new List<string>
            {
                "Task Execution Failure Alert",
                "=============================",
                "",
                $"Box: {boxName}",
                $"Task: {taskName}",
                $"Execution ID: {executionId}",
                ""
            };

            if (boxRunId.HasValue)
            {
                lines.Add($"Box Run ID: {boxRunId}");
                lines.Add("");
            }

            lines.AddRange(new[]
            {
                "Failure Details:",
                "----------------",
                $"Reason: {failureReason}",
                $"Trigger: {triggerSource}",
                ""
            });

            if (scheduledForUtc.HasValue)
            {
                lines.Add($"Scheduled For: {scheduledForUtc:O} UTC");
                lines.Add("");
            }

            if (!string.IsNullOrWhiteSpace(requestedByUsername))
            {
                lines.Add($"Requested By: {requestedByUsername}");
                lines.Add("");
            }

            lines.AddRange(new[]
            {
                "Action Required:",
                "----------------",
                "Please review the execution logs in CHRONIQ for detailed error information.",
                "Determine if this is a temporary issue (retry) or a permanent failure that needs attention.",
                "",
                $"Timestamp: {DateTime.UtcNow:O} UTC",
                "---",
                "This is an automated message from CHRONIQ."
            });

            return string.Join(Environment.NewLine, lines);
        }
    }
}
