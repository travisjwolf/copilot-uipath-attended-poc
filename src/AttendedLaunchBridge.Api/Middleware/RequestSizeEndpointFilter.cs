using AttendedLaunchBridge.Api.Options;
using Microsoft.Extensions.Options;

namespace AttendedLaunchBridge.Api.Middleware;

public sealed class RequestSizeEndpointFilter(IOptions<BridgeSecurityOptions> options)
    : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var contentLength = context.HttpContext.Request.ContentLength;
        if (contentLength > options.Value.MaxRequestBodyBytes)
        {
            return Results.Json(
                new { error = "The request body is too large." },
                statusCode: StatusCodes.Status413PayloadTooLarge);
        }

        return await next(context);
    }
}
