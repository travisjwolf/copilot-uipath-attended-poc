using AttendedLaunchBridge.Api.Domain;

namespace AttendedLaunchBridge.Api.Application.Abstractions;

public interface IJobStatusStore
{
    bool TryAdd(AttendedJob job);

    AttendedJob? Get(Guid requestId);

    AttendedJob? TryUpdate(
        Guid requestId,
        Func<AttendedJob, AttendedJob> update);
}
