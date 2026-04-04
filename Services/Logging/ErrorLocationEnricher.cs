using System.Diagnostics;
using Serilog.Core;
using Serilog.Events;

namespace AScheduler.Services.Logging;

/// <summary>
/// Enriches log events with error location details extracted from exceptions.
/// </summary>
public sealed class ErrorLocationEnricher : ILogEventEnricher
{
    /// <summary>
    /// Adds error metadata for events with exceptions and a log file name for every event.
    /// </summary>
    /// <param name="logEvent">The log event to enrich.</param>
    /// <param name="propertyFactory">Factory used to create log properties.</param>
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        ArgumentNullException.ThrowIfNull(logEvent);
        ArgumentNullException.ThrowIfNull(propertyFactory);

        // Keep an explicit file name property for DB correlation with rolling files.
        var logFileName = $"app-{logEvent.Timestamp.UtcDateTime:yyyyMMdd}.log";
        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("LogFileName", logFileName));

        if (logEvent.Exception is null)
        {
            return;
        }

        var exceptionType = logEvent.Exception.GetType().FullName;
        if (!string.IsNullOrWhiteSpace(exceptionType))
        {
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("ExceptionType", exceptionType));
        }

        var location = ExtractLocation(logEvent.Exception);
        if (!string.IsNullOrWhiteSpace(location.File))
        {
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("ErrorFile", location.File));
        }

        if (!string.IsNullOrWhiteSpace(location.Method))
        {
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("ErrorMethod", location.Method));
        }

        if (location.Line.HasValue && location.Line.Value > 0)
        {
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("ErrorLine", location.Line.Value));
        }
    }

    private static (string? File, string? Method, int? Line) ExtractLocation(Exception exception)
    {
        try
        {
            var trace = new StackTrace(exception, true);
            var frame = trace.GetFrames()?.FirstOrDefault(f => !string.IsNullOrWhiteSpace(f.GetFileName()));
            if (frame is null)
            {
                return (null, null, null);
            }

            var fileName = Path.GetFileName(frame.GetFileName());
            var methodInfo = frame.GetMethod();
            var methodName = methodInfo is null
                ? null
                : $"{methodInfo.DeclaringType?.Name}.{methodInfo.Name}";
            var line = frame.GetFileLineNumber();

            return (fileName, methodName, line > 0 ? line : null);
        }
        catch
        {
            return (null, null, null);
        }
    }
}
