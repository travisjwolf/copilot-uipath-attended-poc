using AttendedLaunchBridge.Api.Domain;

namespace AttendedLaunchBridge.Api.Application.Abstractions;

public interface IRobotProcessProvider
{
    Task<IReadOnlyCollection<RobotProcess>> GetProcessesAsync(
        CancellationToken cancellationToken);
}
