using AttendedLaunchBridge.Api.Domain;

namespace AttendedLaunchBridge.Api.Contracts;

public sealed record JobStatusResponse(
    Guid RequestId,
    string ProcessAlias,
    AttendedJobStatus Status,
    DateTimeOffset CreatedUtc,
    DateTimeOffset? StartedUtc,
    DateTimeOffset? CompletedUtc,
    IReadOnlyDictionary<string, string>? Result,
    string? Error)
{
    public static JobStatusResponse FromJob(AttendedJob job) =>
        new(
            job.RequestId,
            job.ProcessAlias,
            job.Status,
            job.CreatedUtc,
            job.StartedUtc,
            job.CompletedUtc,
            job.Result,
            job.Error);
}
