using AttendedLaunchBridge.Api.Options;
using Microsoft.Extensions.Options;

namespace AttendedLaunchBridge.Api.Middleware;

public sealed class CorrelationIdMiddleware(
    RequestDelegate next,
    IOptions<BridgeSecurityOptions> options,
    ILogger<CorrelationIdMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = Guid.NewGuid().ToString("N");
        context.TraceIdentifier = correlationId;
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[options.Value.CorrelationIdHeaderName] = correlationId;
            return Task.CompletedTask;
        });

        using var scope = logger.BeginScope(
            new Dictionary<string, object>
            {
                ["CorrelationId"] = correlationId
            });

        logger.LogInformation(
            "HTTP {Method} {Path} started with correlation {CorrelationId}.",
            context.Request.Method,
            context.Request.Path,
            correlationId);

        try
        {
            await next(context);
        }
        finally
        {
            logger.LogInformation(
                "HTTP {Method} {Path} completed with status {StatusCode} and correlation {CorrelationId}.",
                context.Request.Method,
                context.Request.Path,
                context.Response.StatusCode,
                correlationId);
        }
    }
}
