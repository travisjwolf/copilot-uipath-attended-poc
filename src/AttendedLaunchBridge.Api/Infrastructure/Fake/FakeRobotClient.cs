using AttendedLaunchBridge.Api.Application.Abstractions;
using AttendedLaunchBridge.Api.Domain;
using AttendedLaunchBridge.Api.Options;
using Microsoft.Extensions.Options;

namespace AttendedLaunchBridge.Api.Infrastructure.Fake;

public sealed class FakeRobotClient(
    IProcessAuthorizationService authorizationService,
    IOptions<UiPathOptions> options) : IRobotProcessProvider, IAttendedJobLauncher
{
    public Task<IReadOnlyCollection<RobotProcess>> GetProcessesAsync(
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        IReadOnlyCollection<RobotProcess> processes = authorizationService
            .GetAllowedProcesses()
            .Select(process => process.RobotProcessName)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(name => new RobotProcess(name))
            .ToArray();

        return Task.FromResult(processes);
    }

    public async Task<JobExecutionResult> LaunchAsync(
        JobLaunchRequest request,
        JobStatusCallback statusChanged,
        CancellationToken cancellationToken)
    {
        await Task.Delay(options.Value.FakeStartDelayMs, cancellationToken);
        await statusChanged(AttendedJobStatus.Running, cancellationToken);
        await Task.Delay(options.Value.FakeExecutionDelayMs, cancellationToken);

        if (request.Inputs.TryGetValue("in_Message", out var message) &&
            message.Equals("[simulate-failure]", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Synthetic Robot failure requested.");
        }

        return new JobExecutionResult(
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["out_Result"] = "Synthetic attended job completed."
            });
    }
}
