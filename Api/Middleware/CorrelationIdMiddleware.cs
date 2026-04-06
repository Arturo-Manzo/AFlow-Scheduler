using System.Security.Claims;
using Serilog.Context;

namespace CHRONIQ.Api.Middleware;

/// <summary>
/// Pushes correlation and request context properties into Serilog LogContext for each HTTP request.
/// </summary>
public sealed class CorrelationIdMiddleware(RequestDelegate next)
{
    private const string CorrelationIdHeader = "X-Correlation-Id";

    /// <summary>
    /// Executes the middleware logic for the current request.
    /// </summary>
    /// <param name="context">Current HTTP context.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = GetOrGenerateCorrelationId(context);
        var requestPath = $"{context.Request.Method} {context.Request.Path}";
        var userId = GetUserId(context);

        context.Items[CorrelationIdHeader] = correlationId;

        context.Response.OnStarting(() =>
        {
            context.Response.Headers[CorrelationIdHeader] = correlationId.ToString();
            return Task.CompletedTask;
        });

        using (LogContext.PushProperty("CorrelationId", correlationId))
        using (LogContext.PushProperty("RequestPath", requestPath))
        using (LogContext.PushProperty("UserId", userId, true))
        {
            await next(context);
        }
    }

    private static Guid GetOrGenerateCorrelationId(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue(CorrelationIdHeader, out var existingId)
            && Guid.TryParse(existingId.ToString(), out var parsedId))
        {
            return parsedId;
        }

        return Guid.NewGuid();
    }

    private static int? GetUserId(HttpContext context)
    {
        var claim = context.User.FindFirst(ClaimTypes.NameIdentifier);
        return claim is not null && int.TryParse(claim.Value, out var userId)
            ? userId
            : null;
    }
}
