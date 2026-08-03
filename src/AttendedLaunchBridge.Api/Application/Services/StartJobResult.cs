using AttendedLaunchBridge.Api.Domain;

namespace AttendedLaunchBridge.Api.Application.Services;

public sealed record StartJobResult(
    AttendedJob? Job,
    StartJobFailure Failure,
    string? Error,
    bool IsReplay = false)
{
    public bool IsAccepted => Job is not null;

    public static StartJobResult Accepted(AttendedJob job) =>
        new(job, StartJobFailure.None, null);

    public static StartJobResult Replayed(AttendedJob job) =>
        new(job, StartJobFailure.None, null, true);

    public static StartJobResult Invalid(string error) =>
        new(null, StartJobFailure.InvalidRequest, error);

    public static StartJobResult UnknownAlias(string error) =>
        new(null, StartJobFailure.UnknownAlias, error);

    public static StartJobResult Fail(StartJobFailure failure, string error) =>
        new(null, failure, error);
}

public enum StartJobFailure
{
    None,
    InvalidRequest,
    UnknownAlias,
    PayloadTooLarge,
    IdempotencyConflict,
    ExpiredRequest,
    ConcurrentJobLimit
}
