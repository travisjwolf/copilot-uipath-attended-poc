namespace AttendedLaunchBridge.Api.Options;

public sealed class UiPathOptions
{
    public const string SectionName = "UiPath";

    public bool UseFakeRobotClient { get; init; }

    public int FakeStartDelayMs { get; init; } = 100;

    public int FakeExecutionDelayMs { get; init; } = 500;

    public Dictionary<string, AllowedProcessOptions> AllowedProcesses { get; init; } = [];
}
