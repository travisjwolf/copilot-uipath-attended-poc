namespace AttendedLaunchBridge.Api.Domain;

public sealed record AuthorizedProcess(
    string Alias,
    string RobotProcessName,
    IReadOnlySet<string> AllowedInputs);
