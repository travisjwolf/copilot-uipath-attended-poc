using AttendedLaunchBridge.Api.Application.Services;
using AttendedLaunchBridge.Api.Contracts;
using AttendedLaunchBridge.Api.Middleware;
using AttendedLaunchBridge.Api.Options;
using Microsoft.Extensions.Options;

namespace AttendedLaunchBridge.Api.Endpoints;

public static class BridgeEndpoints
{
    public static IEndpointRouteBuilder MapBridgeEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(
            "/health",
            () => Results.Ok(new
            {
                status = "Healthy",
                service = "AttendedLaunchBridge.Api"
            }));

        endpoints.MapGet(
            "/api/processes",
            async (
                AttendedJobService service,
                CancellationToken cancellationToken) =>
                Results.Ok(await service.GetProcessesAsync(cancellationToken)));

        var jobs = endpoints
            .MapGroup("/api/jobs")
            .AddEndpointFilter<ApiKeyEndpointFilter>();

        jobs.MapPost(
                string.Empty,
                (
                    StartJobRequest? request,
                    HttpContext context,
                    AttendedJobService service,
                    IOptions<BridgeSecurityOptions> options) =>
                {
                    var settings = options.Value;
                    var result = service.Start(
                        request,
                        context.Request.Headers[settings.IdempotencyKeyHeaderName].ToString(),
                        context.Request.Headers[settings.RequestTimestampHeaderName].ToString(),
                        context.TraceIdentifier);

                    if (result.IsAccepted)
                    {
                        if (result.IsReplay)
                        {
                            context.Response.Headers["Idempotency-Replayed"] = "true";
                        }

                        var job = result.Job!;
                        return Results.Accepted(
                            $"/api/jobs/{job.RequestId}",
                            new StartJobAcceptedResponse(
                                job.RequestId,
                                "Accepted",
                                "The attended automation request was accepted."));
                    }

                    return MapStartFailure(result, context);
                })
            .AddEndpointFilter<RequestSizeEndpointFilter>();

        jobs.MapGet(
            "/{requestId:guid}",
            (Guid requestId, AttendedJobService service) =>
            {
                var job = service.Get(requestId);
                return job is null
                    ? Results.NotFound()
                    : Results.Ok(JobStatusResponse.FromJob(job));
            });

        jobs.MapPost(
            "/{requestId:guid}/cancel",
            (Guid requestId, AttendedJobService service) =>
            {
                var result = service.Cancel(requestId);
                return result switch
                {
                    CancelJobResult.Accepted => Results.Accepted(
                        $"/api/jobs/{requestId}",
                        JobStatusResponse.FromJob(service.Get(requestId)!)),
                    CancelJobResult.NotCancellable => Results.Conflict(
                        new { error = "The job is already in a terminal state." }),
                    _ => Results.NotFound()
                };
            });

        return endpoints;
    }

    private static IResult MapStartFailure(
        StartJobResult result,
        HttpContext context)
    {
        return result.Failure switch
        {
            StartJobFailure.UnknownAlias => Results.NotFound(new { error = result.Error }),
            StartJobFailure.PayloadTooLarge => Results.Json(
                new { error = result.Error },
                statusCode: StatusCodes.Status413PayloadTooLarge),
            StartJobFailure.IdempotencyConflict => Results.Conflict(
                new { error = result.Error }),
            StartJobFailure.ExpiredRequest => Results.Json(
                new { error = result.Error },
                statusCode: StatusCodes.Status408RequestTimeout),
            StartJobFailure.ConcurrentJobLimit => TooManyRequests(result, context),
            _ => Results.BadRequest(new { error = result.Error })
        };
    }

    private static IResult TooManyRequests(
        StartJobResult result,
        HttpContext context)
    {
        context.Response.Headers.RetryAfter = "1";
        return Results.Json(
            new { error = result.Error },
            statusCode: StatusCodes.Status429TooManyRequests);
    }
}
