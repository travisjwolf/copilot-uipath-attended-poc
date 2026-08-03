namespace AttendedLaunchBridge.Api.Domain;

public sealed record JobLaunchRequest(
    Guid RequestId,
    string RobotProcessName,
    IReadOnlyDictionary<string, string> Inputs);
