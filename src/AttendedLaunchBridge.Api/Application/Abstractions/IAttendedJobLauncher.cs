using AttendedLaunchBridge.Api.Domain;

namespace AttendedLaunchBridge.Api.Application.Abstractions;

public delegate ValueTask JobStatusCallback(
    AttendedJobStatus status,
    CancellationToken cancellationToken);

public interface IAttendedJobLauncher
{
    Task<JobExecutionResult> LaunchAsync(
        JobLaunchRequest request,
        JobStatusCallback statusChanged,
        CancellationToken cancellationToken);
}
