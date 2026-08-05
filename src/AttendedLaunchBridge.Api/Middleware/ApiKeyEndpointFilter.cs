using System.Net.Http.Headers;
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
        if (!HasValidApiKeyHeader(context.HttpContext, settings) &&
            !HasValidGatewayBasicCredential(context.HttpContext, settings))
        {
            context.HttpContext.Response.Headers.Append(
                "WWW-Authenticate",
                "ApiKey");
            if (settings.EnableGatewayBasicAuthentication)
            {
                context.HttpContext.Response.Headers.Append(
                    "WWW-Authenticate",
                    "Basic realm=\"CopilotUiPathAttendedPoc\"");
            }

            return Results.Json(
                new { error = "Valid bridge credentials are required." },
                statusCode: StatusCodes.Status401Unauthorized);
        }

        return await next(context);
    }

    private static bool HasValidApiKeyHeader(
        HttpContext context,
        BridgeSecurityOptions settings) =>
        context.Request.Headers.TryGetValue(
            settings.ApiKeyHeaderName,
            out var suppliedValues) &&
        suppliedValues.Count == 1 &&
        IsMatch(suppliedValues[0], settings.ApiKey!);

    private static bool HasValidGatewayBasicCredential(
        HttpContext context,
        BridgeSecurityOptions settings)
    {
        if (!settings.EnableGatewayBasicAuthentication ||
            !AuthenticationHeaderValue.TryParse(
                context.Request.Headers.Authorization,
                out var authorization) ||
            !string.Equals(
                authorization.Scheme,
                "Basic",
                StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(authorization.Parameter))
        {
            return false;
        }

        try
        {
            var decoded = Encoding.UTF8.GetString(
                Convert.FromBase64String(authorization.Parameter));
            var separatorIndex = decoded.IndexOf(':');
            if (separatorIndex < 0)
            {
                return false;
            }

            var username = decoded[..separatorIndex];
            var password = decoded[(separatorIndex + 1)..];
            return IsMatch(username, settings.GatewayBasicUsername) &&
                   IsMatch(password, settings.ApiKey!);
        }
        catch (FormatException)
        {
            return false;
        }
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
