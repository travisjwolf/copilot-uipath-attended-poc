namespace AttendedLaunchBridge.Api.Options;

public sealed class AllowedProcessOptions
{
    public string RobotProcessName { get; init; } = string.Empty;

    public string[] AllowedInputs { get; init; } = [];
}
