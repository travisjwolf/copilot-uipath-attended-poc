using AttendedLaunchBridge.Api.Domain;
using AttendedLaunchBridge.Api.Infrastructure.UiPath;
using Microsoft.Extensions.Logging.Abstractions;
using UiPath.Robot.Api;

namespace AttendedLaunchBridge.Tests;

public sealed class UiPathRobotClientTests
{
    [Fact]
    public async Task GetProcesses_RequestsRefreshAndReturnsRobotNames()
    {
        var robot = new StubRobotClient
        {
            Processes = [new ProcessInfo("process-key", "CopilotAttendedPoC")]
        };
        var client = CreateClient(robot);

        var processes = await client.GetProcessesAsync(CancellationToken.None);

        Assert.True(robot.LastGetProcessesParameters?.ForceRefresh);
        Assert.Equal("CopilotAttendedPoC", Assert.Single(processes).Name);
    }

    [Fact]
    public async Task Launch_ResolvesProcessKeyAndReturnsOutputs()
    {
        var robot = new StubRobotClient
        {
            Processes = [new ProcessInfo("process-key", "CopilotAttendedPoC")],
            RunJobHandler = (job, _) =>
            {
                Assert.Equal("process-key", job.ProcessKey);
                Assert.Equal("synthetic-message", job.InputArguments["in_Message"]);
                return Task.FromResult(
                    new JobOutput(
                        new Dictionary<string, object>
                        {
                            ["out_Result"] = "synthetic-result"
                        }));
            }
        };
        var client = CreateClient(robot);
        var statuses = new List<AttendedJobStatus>();

        var result = await client.LaunchAsync(
            CreateLaunchRequest(),
            (status, _) =>
            {
                statuses.Add(status);
                return ValueTask.CompletedTask;
            },
            CancellationToken.None);

        Assert.Equal([AttendedJobStatus.Running], statuses);
        Assert.Equal("synthetic-result", result.Outputs["out_Result"]);
    }

    [Fact]
    public async Task Launch_RejectsAmbiguousProcessName()
    {
        var robot = new StubRobotClient
        {
            Processes =
            [
                new ProcessInfo("first-key", "CopilotAttendedPoC"),
                new ProcessInfo("second-key", "CopilotAttendedPoC")
            ]
        };
        var client = CreateClient(robot);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => client.LaunchAsync(
                CreateLaunchRequest(),
                (_, _) => ValueTask.CompletedTask,
                CancellationToken.None));

        Assert.Contains("ambiguous", exception.Message);
    }

    [Fact]
    public async Task Launch_PassesCancellationToRobotApi()
    {
        var robot = new StubRobotClient
        {
            Processes = [new ProcessInfo("process-key", "CopilotAttendedPoC")],
            RunJobHandler = async (_, cancellationToken) =>
            {
                await Task.Delay(Timeout.Infinite, cancellationToken);
                throw new InvalidOperationException("Unreachable");
            }
        };
        var client = CreateClient(robot);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => client.LaunchAsync(
                CreateLaunchRequest(),
                (_, _) => ValueTask.CompletedTask,
                cancellation.Token));
    }

    private static UiPathRobotClient CreateClient(IRobotClient robotClient) =>
        new(robotClient, NullLogger<UiPathRobotClient>.Instance);

    private static JobLaunchRequest CreateLaunchRequest() =>
        new(
            Guid.Parse("11111111-2222-3333-4444-555555555555"),
            "CopilotAttendedPoC",
            new Dictionary<string, string>
            {
                ["in_RequestId"] = "11111111-2222-3333-4444-555555555555",
                ["in_Message"] = "synthetic-message",
                ["in_RequestedBy"] = "synthetic-test-user"
            });

    private sealed class StubRobotClient : IRobotClient
    {
        public IReadOnlyCollection<ProcessInfo> Processes { get; init; } = [];

        public GetProcessesParameters? LastGetProcessesParameters { get; private set; }

        public Func<Job, CancellationToken, Task<JobOutput>>? RunJobHandler { get; init; }

        public Task<IReadOnlyCollection<ProcessInfo>> GetProcesses(
            GetProcessesParameters parameters,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastGetProcessesParameters = parameters;
            return Task.FromResult(Processes);
        }

        public Task<JobOutput> RunJob(Job job, CancellationToken cancellationToken) =>
            RunJobHandler?.Invoke(job, cancellationToken) ??
            throw new InvalidOperationException("No synthetic run handler was configured.");

        public Task<string> GetAccessToken(
            bool refresh,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<InstallProcessResult> InstallProcess(
            InstallProcessParameters parameters,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
