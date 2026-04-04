namespace AScheduler.Services
{
    /// <summary>
    /// Service responsible for sending notifications when a task fails within a box.
    /// If a Box has a NotificationEmail configured, this service sends an alert via SMTP.
    /// Failures in notification delivery should not interrupt task execution.
    /// </summary>
    public interface ITaskFailureNotificationService
    {
        /// <summary>
        /// Sends a failure notification email for a task that has failed.
        /// This operation is best-effort: if sending fails, the task execution is NOT affected.
        /// </summary>
        /// <param name="boxId">ID of the box that contains the failed task.</param>
        /// <param name="taskId">ID of the task that failed.</param>
        /// <param name="boxRunId">ID of the box run, if known; null for force-starts without box runs.</param>
        /// <param name="taskName">Human-friendly task name.</param>
        /// <param name="boxName">Human-friendly box name.</param>
        /// <param name="notificationEmail">Email address to send the alert to.</param>
        /// <param name="failureReason">Description of why the task failed (e.g., "exit code 1" or "timeout after 60s").</param>
        /// <param name="executionId">ID of the task execution record for reference.</param>
        /// <param name="triggerSource">How the box was triggered (schedule, manual, force-start, etc.).</param>
        /// <param name="scheduledForUtc">When the box was scheduled to run (if scheduled).</param>
        /// <param name="requestedByUsername">Who requested the execution, if applicable.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>True if notification was sent successfully; false if send failed (but does not throw).</returns>
        Task<bool> SendTaskFailureNotificationAsync(
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
            CancellationToken cancellationToken);
    }
}
