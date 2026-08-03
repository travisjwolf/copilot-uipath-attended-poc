using AttendedLaunchBridge.Api.Domain;

namespace AttendedLaunchBridge.Api.Application.Abstractions;

public interface IProcessAuthorizationService
{
    ProcessAuthorizationResult Authorize(
        string processAlias,
        IEnumerable<string> inputNames);

    IReadOnlyCollection<AuthorizedProcess> GetAllowedProcesses();
}
