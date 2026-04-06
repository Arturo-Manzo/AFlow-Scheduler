namespace CHRONIQ.Domain
{
    /// <summary>
    /// Canonical trigger-source values used across box and task execution flows.
    /// </summary>
    public static class TriggerSources
    {
        public const string Scheduler = "Scheduler";
        public const string Manual = "Manual";
        public const string ForceStart = "ForceStart";
        public const string Retry = "Retry";

        /// <summary>
        /// Normalizes legacy or inconsistent values to canonical trigger sources.
        /// </summary>
        public static string Normalize(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return Scheduler;

            var trimmed = value.Trim();

            if (trimmed.Equals("Scheduled", StringComparison.OrdinalIgnoreCase))
                return Scheduler;
            if (trimmed.Equals(Scheduler, StringComparison.OrdinalIgnoreCase))
                return Scheduler;
            if (trimmed.Equals(Manual, StringComparison.OrdinalIgnoreCase))
                return Manual;
            if (trimmed.Equals(ForceStart, StringComparison.OrdinalIgnoreCase))
                return ForceStart;
            if (trimmed.Equals(Retry, StringComparison.OrdinalIgnoreCase))
                return Retry;

            return trimmed;
        }
    }
}