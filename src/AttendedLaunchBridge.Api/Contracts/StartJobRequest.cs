namespace AttendedLaunchBridge.Api.Contracts;

public sealed record StartJobRequest(
    string? ProcessAlias,
    string? RequestedBy,
    Dictionary<string, string?>? Inputs);
