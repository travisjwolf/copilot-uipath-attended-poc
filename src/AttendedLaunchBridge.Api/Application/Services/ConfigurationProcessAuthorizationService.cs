using AttendedLaunchBridge.Api.Application.Abstractions;
using AttendedLaunchBridge.Api.Domain;
using AttendedLaunchBridge.Api.Options;
using Microsoft.Extensions.Options;

namespace AttendedLaunchBridge.Api.Application.Services;

public sealed class ConfigurationProcessAuthorizationService : IProcessAuthorizationService
{
    private readonly IReadOnlyDictionary<string, AuthorizedProcess> _allowedProcesses;

    public ConfigurationProcessAuthorizationService(IOptions<UiPathOptions> options)
    {
        _allowedProcesses = options.Value.AllowedProcesses.ToDictionary(
            process => process.Key,
            process => new AuthorizedProcess(
                process.Key,
                process.Value.RobotProcessName,
                process.Value.AllowedInputs.ToHashSet(StringComparer.Ordinal)),
            StringComparer.OrdinalIgnoreCase);
    }

    public ProcessAuthorizationResult Authorize(
        string processAlias,
        IEnumerable<string> inputNames)
    {
        if (!_allowedProcesses.TryGetValue(processAlias, out var process))
        {
            return ProcessAuthorizationResult.Denied(
                ProcessAuthorizationFailure.UnknownAlias,
                $"Process alias '{processAlias}' is not approved.");
        }

        var unexpectedInputs = inputNames
            .Where(input => !process.AllowedInputs.Contains(input))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(input => input, StringComparer.Ordinal)
            .ToArray();

        return unexpectedInputs.Length == 0
            ? ProcessAuthorizationResult.Allowed(process)
            : ProcessAuthorizationResult.Denied(
                ProcessAuthorizationFailure.UnexpectedInput,
                $"Unexpected input argument(s): {string.Join(", ", unexpectedInputs)}.");
    }

    public IReadOnlyCollection<AuthorizedProcess> GetAllowedProcesses() =>
        _allowedProcesses.Values.ToArray();
}
