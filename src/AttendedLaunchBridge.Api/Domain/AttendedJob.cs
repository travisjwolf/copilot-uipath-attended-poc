namespace AttendedLaunchBridge.Api.Domain;

public sealed record AttendedJob(
    Guid RequestId,
    string CorrelationId,
    string ProcessAlias,
    string RobotProcessName,
    string RequestedBy,
    IReadOnlyDictionary<string, string> Inputs,
    AttendedJobStatus Status,
    DateTimeOffset CreatedUtc,
    DateTimeOffset RequestExpiresUtc,
    DateTimeOffset? StartedUtc,
    DateTimeOffset? CompletedUtc,
    IReadOnlyDictionary<string, string>? Result,
    string? Error);
