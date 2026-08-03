using AttendedLaunchBridge.Api.Application.Abstractions;
using AttendedLaunchBridge.Api.Domain;
using UiPath.Robot.Api;

namespace AttendedLaunchBridge.Api.Infrastructure.UiPath;

public sealed class UiPathRobotClient : IRobotProcessProvider, IAttendedJobLauncher
{
    private readonly IRobotClient _robotClient;
    private readonly ILogger<UiPathRobotClient> _logger;

    public UiPathRobotClient(ILogger<UiPathRobotClient> logger)
        : this(
            new RobotClient(
                new RobotClientSettings
                {
                    EventScheduler = TaskScheduler.Default
                }),
            logger)
    {
    }

    public UiPathRobotClient(
        IRobotClient robotClient,
        ILogger<UiPathRobotClient> logger)
    {
        _robotClient = robotClient;
        _logger = logger;
    }

    public async Task<IReadOnlyCollection<RobotProcess>> GetProcessesAsync(
        CancellationToken cancellationToken)
    {
        var processes = await GetRobotProcessesAsync(
            forceRefresh: true,
            cancellationToken);

        return processes
            .Where(process => !string.IsNullOrWhiteSpace(process.Name))
            .Select(process => new RobotProcess(process.Name))
            .ToArray();
    }

    public async Task<JobExecutionResult> LaunchAsync(
        JobLaunchRequest request,
        JobStatusCallback statusChanged,
        CancellationToken cancellationToken)
    {
        var processes = await GetRobotProcessesAsync(
            forceRefresh: false,
            cancellationToken);
        var matches = processes
            .Where(process => process.Name.Equals(
                request.RobotProcessName,
                StringComparison.OrdinalIgnoreCase))
            .Take(2)
            .ToArray();

        if (matches.Length == 0)
        {
            throw new InvalidOperationException(
                $"The configured UiPath process '{request.RobotProcessName}' is not available to the local Robot.");
        }

        if (matches.Length > 1)
        {
            throw new InvalidOperationException(
                $"The configured UiPath process name '{request.RobotProcessName}' is ambiguous.");
        }

        var job = new Job(matches[0])
        {
            InputArguments = request.Inputs.ToDictionary(
                input => input.Key,
                input => (object)input.Value,
                StringComparer.Ordinal)
        };

        EventHandler<JobStatusChangedArgs> statusHandler = (_, args) =>
            _logger.LogInformation(
                "UiPath job for request {RequestId} reported Robot status {RobotStatus}.",
                request.RequestId,
                args.Status);
        job.StatusChanged += statusHandler;

        try
        {
            await statusChanged(AttendedJobStatus.Running, cancellationToken);
            var output = await _robotClient.RunJob(job, cancellationToken);

            return new JobExecutionResult(
                output.Arguments.ToDictionary(
                    argument => argument.Key,
                    argument => Convert.ToString(argument.Value) ?? string.Empty,
                    StringComparer.Ordinal));
        }
        catch (Exception exception) when (exception.GetJobException() is not null)
        {
            _logger.LogError(
                "UiPath workflow execution failed for request {RequestId}.",
                request.RequestId);
            throw;
        }
        finally
        {
            job.StatusChanged -= statusHandler;
        }
    }

    private Task<IReadOnlyCollection<ProcessInfo>> GetRobotProcessesAsync(
        bool forceRefresh,
        CancellationToken cancellationToken) =>
        _robotClient.GetProcesses(
            new GetProcessesParameters
            {
                ForceRefresh = forceRefresh
            },
            cancellationToken);
}
