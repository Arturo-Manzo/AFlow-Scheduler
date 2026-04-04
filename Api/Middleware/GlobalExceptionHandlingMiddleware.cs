using System.Net;
using System.Text.Json;
using Serilog.Context;

namespace AScheduler.Api.Middleware;

/// <summary>
/// Captures unhandled exceptions and returns a consistent ProblemDetails response.
/// </summary>
public sealed class GlobalExceptionHandlingMiddleware(RequestDelegate next, ILogger<GlobalExceptionHandlingMiddleware> logger)
{
    /// <summary>
    /// Executes the middleware logic for the current HTTP request.
    /// </summary>
    /// <param name="context">The HTTP context for the current request.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception exception)
        {
            using (LogContext.PushProperty("StatusCode", StatusCodes.Status500InternalServerError))
            {
                logger.LogError(exception, "Unhandled exception while processing request {Method} {Path}", context.Request.Method, context.Request.Path);
            }

            if (context.Response.HasStarted)
            {
                throw;
            }

            context.Response.Clear();
            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
            context.Response.ContentType = "application/problem+json";

            var problemDetails = new
            {
                type = "https://httpstatuses.com/500",
                title = "An unexpected error occurred.",
                status = StatusCodes.Status500InternalServerError,
                detail = "The server encountered an unexpected condition.",
                traceId = context.TraceIdentifier
            };

            await context.Response.WriteAsync(JsonSerializer.Serialize(problemDetails));
        }
    }
}
