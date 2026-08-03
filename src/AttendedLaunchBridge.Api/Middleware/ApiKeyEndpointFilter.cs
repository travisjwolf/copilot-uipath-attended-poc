using System.Security.Cryptography;
using System.Text;
using AttendedLaunchBridge.Api.Options;
using Microsoft.Extensions.Options;

namespace AttendedLaunchBridge.Api.Middleware;

public sealed class ApiKeyEndpointFilter(IOptions<BridgeSecurityOptions> options)
    : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var settings = options.Value;
        if (!context.HttpContext.Request.Headers.TryGetValue(
                settings.ApiKeyHeaderName,
                out var suppliedValues) ||
            suppliedValues.Count != 1 ||
            !IsMatch(suppliedValues[0], settings.ApiKey!))
        {
            context.HttpContext.Response.Headers.WWWAuthenticate = "ApiKey";
            return Results.Json(
                new { error = "A valid API key is required." },
                statusCode: StatusCodes.Status401Unauthorized);
        }

        return await next(context);
    }

    private static bool IsMatch(string? supplied, string expected)
    {
        if (string.IsNullOrEmpty(supplied))
        {
            return false;
        }

        var suppliedHash = SHA256.HashData(Encoding.UTF8.GetBytes(supplied));
        var expectedHash = SHA256.HashData(Encoding.UTF8.GetBytes(expected));
        return CryptographicOperations.FixedTimeEquals(suppliedHash, expectedHash);
    }
}
