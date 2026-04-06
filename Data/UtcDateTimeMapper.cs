namespace CHRONIQ.Data
{
    internal static class UtcDateTimeMapper
    {
        public static DateTime EnsureUtc(DateTime value) => value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };

        public static DateTime? EnsureUtc(DateTime? value) => value.HasValue
            ? EnsureUtc(value.Value)
            : null;
    }
}