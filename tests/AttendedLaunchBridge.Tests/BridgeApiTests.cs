using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using AttendedLaunchBridge.Api.Contracts;
using AttendedLaunchBridge.Api.Domain;
using AttendedLaunchBridge.Api.Options;

namespace AttendedLaunchBridge.Tests;

public sealed class BridgeApiTests : IDisposable
{
    private readonly BridgeWebApplicationFactory _factory = new();
    private readonly HttpClient _client;

    public BridgeApiTests()
    {
        _client = _factory.CreateClient();
        _client.DefaultRequestHeaders.Add(
            BridgeSecurityOptions.DefaultApiKeyHeaderName,
            BridgeWebApplicationFactory.ApiKey);
    }

    [Fact]
    public async Task Health_ReturnsHealthy()
    {
        var response = await _client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var health = await response.Content.ReadFromJsonAsync<HealthResponse>();
        Assert.Equal("Healthy", health?.Status);
        Assert.True(response.Headers.Contains(
            BridgeSecurityOptions.DefaultCorrelationIdHeaderName));
    }

    [Fact]
    public async Task Processes_ReturnsApprovedAlias()
    {
        var processes = await _client.GetFromJsonAsync<ProcessSummaryResponse[]>(
            "/api/processes");

        var process = Assert.Single(processes!);
        Assert.Equal("CopilotAttendedPoC", process.ProcessAlias);
    }

    [Fact]
    public async Task Jobs_ExecutesFakeProcessToCompletion()
    {
        var accepted = await StartJobAsync("CopilotAttendedPoC");

        var status = await WaitForTerminalStatusAsync(accepted.RequestId);

        Assert.Equal(AttendedJobStatus.Completed, status.Status);
        Assert.Equal(
            "Synthetic attended job completed.",
            status.Result?["out_Result"]);
    }

    [Fact]
    public async Task Jobs_RejectsUnknownProcessAlias()
    {
        var response = await SendStartJobAsync(CreateRequest("NotApproved"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Jobs_RejectsUnexpectedInputArgument()
    {
        var request = CreateRequest("CopilotAttendedPoC");
        request.Inputs!["in_Unexpected"] = "synthetic-value";

        var response = await SendStartJobAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Jobs_RequiresApiKey()
    {
        using var clientWithoutApiKey = _factory.CreateClient();
        using var request = CreateStartJobMessage(CreateRequest("CopilotAttendedPoC"));

        var response = await clientWithoutApiKey.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Contains(
            response.Headers.WwwAuthenticate,
            value => value.Scheme == "ApiKey");
        Assert.Contains(
            response.Headers.WwwAuthenticate,
            value => value.Scheme == "Basic");
    }

    [Fact]
    public async Task Jobs_AcceptsGatewayBasicAuthentication()
    {
        using var gatewayClient = _factory.CreateClient();
        var credentials = Convert.ToBase64String(
            Encoding.UTF8.GetBytes(
                $"CopilotGateway:{BridgeWebApplicationFactory.ApiKey}"));
        gatewayClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Basic", credentials);
        using var request = CreateStartJobMessage(CreateRequest("CopilotAttendedPoC"));

        var response = await gatewayClient.SendAsync(request);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var accepted = await ReadAcceptedAsync(response);
        await gatewayClient.PostAsync($"/api/jobs/{accepted.RequestId}/cancel", null);
    }

    [Fact]
    public async Task Jobs_RejectsInvalidGatewayBasicAuthentication()
    {
        using var gatewayClient = _factory.CreateClient();
        var credentials = Convert.ToBase64String(
            Encoding.UTF8.GetBytes("CopilotGateway:incorrect-synthetic-key"));
        gatewayClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Basic", credentials);
        using var request = CreateStartJobMessage(CreateRequest("CopilotAttendedPoC"));

        var response = await gatewayClient.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Jobs_ReplaysDuplicateIdempotencyKeyWithoutExecutingTwice()
    {
        var idempotencyKey = Guid.NewGuid().ToString("N");
        var request = CreateRequest("CopilotAttendedPoC");

        var firstResponse = await SendStartJobAsync(request, idempotencyKey);
        var secondResponse = await SendStartJobAsync(request, idempotencyKey);
        var first = await ReadAcceptedAsync(firstResponse);
        var second = await ReadAcceptedAsync(secondResponse);

        Assert.Equal(first.RequestId, second.RequestId);
        Assert.True(secondResponse.Headers.TryGetValues(
            "Idempotency-Replayed",
            out var replayed));
        Assert.Equal("true", Assert.Single(replayed));

        await _client.PostAsync($"/api/jobs/{first.RequestId}/cancel", null);
    }

    [Fact]
    public async Task Jobs_TransitionsFromPendingThroughRunningToCompleted()
    {
        var accepted = await StartJobAsync("CopilotAttendedPoC");

        var pending = await GetStatusAsync(accepted.RequestId);
        var running = await WaitForStatusAsync(
            accepted.RequestId,
            AttendedJobStatus.Running);
        var completed = await WaitForTerminalStatusAsync(accepted.RequestId);

        Assert.Equal(AttendedJobStatus.Pending, pending.Status);
        Assert.NotNull(running.StartedUtc);
        Assert.Equal(AttendedJobStatus.Completed, completed.Status);
        Assert.NotNull(completed.CompletedUtc);
    }

    [Fact]
    public async Task Jobs_CancelsRunningFakeProcess()
    {
        var accepted = await StartJobAsync("CopilotAttendedPoC");
        await WaitForStatusAsync(accepted.RequestId, AttendedJobStatus.Running);

        var cancelResponse = await _client.PostAsync(
            $"/api/jobs/{accepted.RequestId}/cancel",
            null);

        Assert.Equal(HttpStatusCode.Accepted, cancelResponse.StatusCode);
        var status = await _client.GetFromJsonAsync<JobStatusResponse>(
            $"/api/jobs/{accepted.RequestId}");
        Assert.Equal(AttendedJobStatus.Cancelled, status?.Status);
    }

    [Fact]
    public async Task Jobs_RecordsSimulatedRobotFailure()
    {
        var request = CreateRequest("CopilotAttendedPoC");
        request.Inputs!["in_Message"] = "[simulate-failure]";
        var response = await SendStartJobAsync(request);
        var accepted = await ReadAcceptedAsync(response);

        var status = await WaitForTerminalStatusAsync(accepted.RequestId);

        Assert.Equal(AttendedJobStatus.Failed, status.Status);
        Assert.Equal("The attended automation failed.", status.Error);
    }

    [Fact]
    public async Task Jobs_RejectsConcurrentAttendedJob()
    {
        var first = await StartJobAsync("CopilotAttendedPoC");

        var secondResponse = await SendStartJobAsync(
            CreateRequest("CopilotAttendedPoC"));

        Assert.Equal(HttpStatusCode.TooManyRequests, secondResponse.StatusCode);
        Assert.Equal(TimeSpan.FromSeconds(1), secondResponse.Headers.RetryAfter?.Delta);
        await _client.PostAsync($"/api/jobs/{first.RequestId}/cancel", null);
    }

    [Fact]
    public async Task Jobs_RejectsExpiredRequest()
    {
        var expiredTimestamp = DateTimeOffset.UtcNow.AddMinutes(-2);

        var response = await SendStartJobAsync(
            CreateRequest("CopilotAttendedPoC"),
            requestTimestamp: expiredTimestamp);

        Assert.Equal(HttpStatusCode.RequestTimeout, response.StatusCode);
    }

    private async Task<StartJobAcceptedResponse> StartJobAsync(string processAlias)
    {
        var response = await SendStartJobAsync(CreateRequest(processAlias));
        return await ReadAcceptedAsync(response);
    }

    private static async Task<StartJobAcceptedResponse> ReadAcceptedAsync(
        HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

        return (await response.Content.ReadFromJsonAsync<StartJobAcceptedResponse>())!;
    }

    private Task<HttpResponseMessage> SendStartJobAsync(
        StartJobRequest request,
        string? idempotencyKey = null,
        DateTimeOffset? requestTimestamp = null)
    {
        var message = CreateStartJobMessage(
            request,
            idempotencyKey,
            requestTimestamp);
        return _client.SendAsync(message);
    }

    private static HttpRequestMessage CreateStartJobMessage(
        StartJobRequest request,
        string? idempotencyKey = null,
        DateTimeOffset? requestTimestamp = null)
    {
        var message = new HttpRequestMessage(HttpMethod.Post, "/api/jobs")
        {
            Content = JsonContent.Create(request)
        };
        message.Headers.Add(
            BridgeSecurityOptions.DefaultIdempotencyKeyHeaderName,
            idempotencyKey ?? Guid.NewGuid().ToString("N"));
        message.Headers.Add(
            BridgeSecurityOptions.DefaultRequestTimestampHeaderName,
            (requestTimestamp ?? DateTimeOffset.UtcNow).ToString("O"));
        return message;
    }

    private Task<JobStatusResponse> GetStatusAsync(Guid requestId) =>
        GetRequiredStatusAsync($"/api/jobs/{requestId}");

    private async Task<JobStatusResponse> GetRequiredStatusAsync(string uri) =>
        (await _client.GetFromJsonAsync<JobStatusResponse>(uri))!;

    private async Task<JobStatusResponse> WaitForStatusAsync(
        Guid requestId,
        AttendedJobStatus expected)
    {
        for (var attempt = 0; attempt < 100; attempt++)
        {
            var status = await GetStatusAsync(requestId);
            if (status.Status == expected)
            {
                return status;
            }

            if (status.Status is AttendedJobStatus.Completed
                or AttendedJobStatus.Failed
                or AttendedJobStatus.Cancelled)
            {
                throw new InvalidOperationException(
                    $"Job reached {status.Status} before {expected}.");
            }

            await Task.Delay(10);
        }

        throw new TimeoutException($"The fake job did not reach {expected}.");
    }

    private async Task<JobStatusResponse> WaitForTerminalStatusAsync(Guid requestId)
    {
        for (var attempt = 0; attempt < 50; attempt++)
        {
            var status = await GetStatusAsync(requestId);

            if (status.Status is AttendedJobStatus.Completed
                or AttendedJobStatus.Failed
                or AttendedJobStatus.Cancelled)
            {
                return status;
            }

            await Task.Delay(25);
        }

        throw new TimeoutException("The fake job did not reach a terminal state.");
    }

    private static StartJobRequest CreateRequest(string processAlias) =>
        new(
            processAlias,
            "synthetic-test-user",
            new Dictionary<string, string?>
            {
                ["in_Message"] = "Synthetic test message.",
                ["in_RequestedBy"] = "synthetic-test-user"
            });

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    private sealed record HealthResponse(string Status, string Service);
}
