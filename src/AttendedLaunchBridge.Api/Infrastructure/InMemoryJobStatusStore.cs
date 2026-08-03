using System.Collections.Concurrent;
using AttendedLaunchBridge.Api.Application.Abstractions;
using AttendedLaunchBridge.Api.Domain;

namespace AttendedLaunchBridge.Api.Infrastructure;

public sealed class InMemoryJobStatusStore : IJobStatusStore
{
    private readonly ConcurrentDictionary<Guid, AttendedJob> _jobs = new();

    public bool TryAdd(AttendedJob job) => _jobs.TryAdd(job.RequestId, job);

    public AttendedJob? Get(Guid requestId) =>
        _jobs.TryGetValue(requestId, out var job) ? job : null;

    public AttendedJob? TryUpdate(
        Guid requestId,
        Func<AttendedJob, AttendedJob> update)
    {
        while (_jobs.TryGetValue(requestId, out var current))
        {
            var updated = update(current);
            if (_jobs.TryUpdate(requestId, updated, current))
            {
                return updated;
            }
        }

        return null;
    }
}
