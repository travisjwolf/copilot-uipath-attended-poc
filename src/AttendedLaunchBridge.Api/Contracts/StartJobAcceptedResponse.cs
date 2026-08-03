namespace AttendedLaunchBridge.Api.Contracts;

public sealed record StartJobAcceptedResponse(
    Guid RequestId,
    string Status,
    string Message);
