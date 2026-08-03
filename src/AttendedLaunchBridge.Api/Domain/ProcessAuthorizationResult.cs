namespace AttendedLaunchBridge.Api.Domain;

public sealed record ProcessAuthorizationResult(
    AuthorizedProcess? Process,
    ProcessAuthorizationFailure Failure,
    string? Error)
{
    public bool IsAuthorized => Process is not null;

    public static ProcessAuthorizationResult Allowed(AuthorizedProcess process) =>
        new(process, ProcessAuthorizationFailure.None, null);

    public static ProcessAuthorizationResult Denied(
        ProcessAuthorizationFailure failure,
        string error) =>
        new(null, failure, error);
}

public enum ProcessAuthorizationFailure
{
    None,
    UnknownAlias,
    UnexpectedInput
}
