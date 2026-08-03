namespace AttendedLaunchBridge.Api.Domain;

public sealed record JobExecutionResult(
    IReadOnlyDictionary<string, string> Outputs);
