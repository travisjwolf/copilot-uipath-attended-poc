using System.Collections.Concurrent;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using AttendedLaunchBridge.Api.Application.Abstractions;
using AttendedLaunchBridge.Api.Contracts;
using AttendedLaunchBridge.Api.Domain;
using AttendedLaunchBridge.Api.Options;
using Microsoft.Extensions.Options;

namespace AttendedLaunchBridge.Api.Application.Services;

public sealed class AttendedJobService
{
    private readonly IRobotProcessProvider _processProvider;
    private readonly IAttendedJobLauncher _jobLauncher;
    private readonly IJobStatusStore _statusStore;
    private readonly IProcessAuthorizationService _authorizationService;
    private readonly BridgeSecurityOptions _securityOptions;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<AttendedJobService> _logger;
    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _activeJobs = new();
    private readonly ConcurrentDictionary<string, IdempotencyEntry> _idempotencyEntries = new();
    private readonly SemaphoreSlim _executionGate;
    private readonly object _startLock = new();

    public AttendedJobService(
        IRobotProcessProvider processProvider,
        IAttendedJobLauncher jobLauncher,
        IJobStatusStore statusStore,
        IProcessAuthorizationService authorizationService,
        IOptions<BridgeSecurityOptions> securityOptions,
        TimeProvider timeProvider,
        ILogger<AttendedJobService> logger)
    {
        _processProvider = processProvider;
        _jobLauncher = jobLauncher;
        _statusStore = statusStore;
        _authorizationService = authorizationService;
        _securityOptions = securityOptions.Value;
        _timeProvider = timeProvider;
        _logger = logger;
        _executionGate = new SemaphoreSlim(
            _securityOptions.MaxConcurrentJobs,
            _securityOptions.MaxConcurrentJobs);
    }

    public async Task<IReadOnlyCollection<ProcessSummaryResponse>> GetProcessesAsync(
        CancellationToken cancellationToken)
    {
        var discovered = await _processProvider.GetProcessesAsync(cancellationToken);
        var discoveredNames = discovered
            .Select(process => process.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return _authorizationService.GetAllowedProcesses()
            .Where(process => discoveredNames.Contains(process.RobotProcessName))
            .Select(process => new ProcessSummaryResponse(process.Alias))
            .ToArray();
    }

    public StartJobResult Start(
        StartJobRequest? request,
        string? idempotencyKey,
        string? requestTimestamp,
        string correlationId)
    {
        var now = _timeProvider.GetUtcNow();
        var validation = ValidateRequest(
            request,
            idempotencyKey,
            requestTimestamp,
            now,
            out var requestExpiresUtc);
        if (validation is not null)
        {
            return validation;
        }

        var authorization = _authorizationService.Authorize(
            request!.ProcessAlias!,
            request.Inputs!.Keys);
        if (!authorization.IsAuthorized)
        {
            return authorization.Failure == ProcessAuthorizationFailure.UnknownAlias
                ? StartJobResult.UnknownAlias(authorization.Error!)
                : StartJobResult.Invalid(authorization.Error!);
        }

        var fingerprint = ComputeRequestFingerprint(request);
        var keyHash = ComputeHash(idempotencyKey!);

        lock (_startLock)
        {
            RemoveExpiredIdempotencyEntries(now);

            if (_idempotencyEntries.TryGetValue(keyHash, out var existing))
            {
                if (!CryptographicOperations.FixedTimeEquals(
                        Convert.FromHexString(existing.RequestFingerprint),
                        Convert.FromHexString(fingerprint)))
                {
                    return StartJobResult.Fail(
                        StartJobFailure.IdempotencyConflict,
                        "The idempotency key was already used for a different request.");
                }

                var existingJob = _statusStore.Get(existing.RequestId);
                if (existingJob is not null)
                {
                    return StartJobResult.Replayed(existingJob);
                }

                _idempotencyEntries.TryRemove(keyHash, out _);
            }

            if (!_executionGate.Wait(0))
            {
                return StartJobResult.Fail(
                    StartJobFailure.ConcurrentJobLimit,
                    "The maximum number of attended jobs is already running.");
            }

            var requestId = Guid.NewGuid();
            var inputs = request.Inputs.ToDictionary(
                input => input.Key,
                input => input.Value!,
                StringComparer.Ordinal);
            inputs["in_RequestId"] = requestId.ToString();

            var job = new AttendedJob(
                requestId,
                correlationId,
                authorization.Process!.Alias,
                authorization.Process.RobotProcessName,
                request.RequestedBy!,
                inputs,
                AttendedJobStatus.Pending,
                now,
                requestExpiresUtc,
                null,
                null,
                null,
                null);

            if (!_statusStore.TryAdd(job))
            {
                _executionGate.Release();
                return StartJobResult.Invalid(
                    "The generated request identifier was not unique.");
            }

            _idempotencyEntries[keyHash] = new IdempotencyEntry(
                requestId,
                fingerprint,
                now.AddMinutes(_securityOptions.IdempotencyRetentionMinutes));

            var cancellation = new CancellationTokenSource();
            _activeJobs[requestId] = cancellation;
            _ = ExecuteAsync(job, cancellation);

            _logger.LogInformation(
                "Accepted attended job {RequestId} for alias {ProcessAlias} with correlation {CorrelationId}.",
                requestId,
                job.ProcessAlias,
                correlationId);

            return StartJobResult.Accepted(job);
        }
    }

    public AttendedJob? Get(Guid requestId) => _statusStore.Get(requestId);

    public CancelJobResult Cancel(Guid requestId)
    {
        var job = _statusStore.Get(requestId);
        if (job is null)
        {
            return CancelJobResult.NotFound;
        }

        if (IsTerminal(job.Status))
        {
            return CancelJobResult.NotCancellable;
        }

        if (_activeJobs.TryGetValue(requestId, out var cancellation))
        {
            try
            {
                cancellation.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // Execution reached its terminal state between the status check and cancellation.
            }
        }

        var updated = _statusStore.TryUpdate(
            requestId,
            current => IsTerminal(current.Status)
                ? current
                : current with
                {
                    Status = AttendedJobStatus.Cancelled,
                    CompletedUtc = _timeProvider.GetUtcNow()
                });

        if (updated?.Status != AttendedJobStatus.Cancelled)
        {
            return CancelJobResult.NotCancellable;
        }

        _logger.LogInformation(
            "Cancelled attended job {RequestId} with correlation {CorrelationId}.",
            requestId,
            job.CorrelationId);
        return CancelJobResult.Accepted;
    }

    private StartJobResult? ValidateRequest(
        StartJobRequest? request,
        string? idempotencyKey,
        string? requestTimestamp,
        DateTimeOffset now,
        out DateTimeOffset requestExpiresUtc)
    {
        requestExpiresUtc = default;

        if (request is null)
        {
            return StartJobResult.Invalid("A JSON request body is required.");
        }

        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return StartJobResult.Invalid("An Idempotency-Key header is required.");
        }

        if (idempotencyKey.Length > _securityOptions.MaxIdempotencyKeyLength)
        {
            return StartJobResult.Fail(
                StartJobFailure.PayloadTooLarge,
                "The idempotency key is too large.");
        }

        if (!DateTimeOffset.TryParse(
                requestTimestamp,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out var requestedUtc))
        {
            return StartJobResult.Invalid(
                "An ISO-8601 X-Request-Timestamp header is required.");
        }

        if (requestedUtc > now.AddSeconds(_securityOptions.AllowedClockSkewSeconds))
        {
            return StartJobResult.Invalid("The request timestamp is in the future.");
        }

        requestExpiresUtc = requestedUtc.AddSeconds(_securityOptions.RequestMaxAgeSeconds);
        if (now > requestExpiresUtc.AddSeconds(_securityOptions.AllowedClockSkewSeconds))
        {
            return StartJobResult.Fail(
                StartJobFailure.ExpiredRequest,
                "The request has expired.");
        }

        if (string.IsNullOrWhiteSpace(request.ProcessAlias) ||
            string.IsNullOrWhiteSpace(request.RequestedBy))
        {
            return StartJobResult.Invalid(
                "processAlias and requestedBy are required.");
        }

        if (request.ProcessAlias.Length > _securityOptions.MaxProcessAliasLength ||
            request.RequestedBy.Length > _securityOptions.MaxRequestedByLength)
        {
            return StartJobResult.Fail(
                StartJobFailure.PayloadTooLarge,
                "A request field exceeds its configured size limit.");
        }

        if (request.Inputs is null || request.Inputs.Any(input => input.Value is null))
        {
            return StartJobResult.Invalid(
                "inputs must contain non-null string values.");
        }

        if (request.Inputs.Count > _securityOptions.MaxInputCount)
        {
            return StartJobResult.Fail(
                StartJobFailure.PayloadTooLarge,
                "The request contains too many input arguments.");
        }

        long totalCharacters = 0;
        foreach (var input in request.Inputs)
        {
            if (string.IsNullOrWhiteSpace(input.Key))
            {
                return StartJobResult.Invalid("Input argument names cannot be empty.");
            }

            if (input.Key.Length > _securityOptions.MaxInputNameLength ||
                input.Value!.Length > _securityOptions.MaxInputValueLength)
            {
                return StartJobResult.Fail(
                    StartJobFailure.PayloadTooLarge,
                    "An input argument exceeds its configured size limit.");
            }

            totalCharacters += input.Key.Length + input.Value.Length;
        }

        return totalCharacters > _securityOptions.MaxTotalInputCharacters
            ? StartJobResult.Fail(
                StartJobFailure.PayloadTooLarge,
                "The combined input arguments exceed the configured size limit.")
            : null;
    }

    private async Task ExecuteAsync(
        AttendedJob job,
        CancellationTokenSource cancellation)
    {
        using var scope = _logger.BeginScope(
            new Dictionary<string, object>
            {
                ["CorrelationId"] = job.CorrelationId,
                ["RequestId"] = job.RequestId
            });

        try
        {
            if (_timeProvider.GetUtcNow() > job.RequestExpiresUtc)
            {
                _statusStore.TryUpdate(
                    job.RequestId,
                    current => current with
                    {
                        Status = AttendedJobStatus.Failed,
                        CompletedUtc = _timeProvider.GetUtcNow(),
                        Error = "The request expired before execution."
                    });
                return;
            }

            var launchRequest = new JobLaunchRequest(
                job.RequestId,
                job.RobotProcessName,
                job.Inputs);

            var result = await _jobLauncher.LaunchAsync(
                launchRequest,
                (status, _) =>
                {
                    _statusStore.TryUpdate(
                        job.RequestId,
                        current => current.Status == AttendedJobStatus.Cancelled
                            ? current
                            : current with
                            {
                                Status = status,
                                StartedUtc = status == AttendedJobStatus.Running
                                    ? current.StartedUtc ?? _timeProvider.GetUtcNow()
                                    : current.StartedUtc
                            });

                    _logger.LogInformation(
                        "Attended job {RequestId} changed to {Status}.",
                        job.RequestId,
                        status);
                    return ValueTask.CompletedTask;
                },
                cancellation.Token);

            _statusStore.TryUpdate(
                job.RequestId,
                current => current.Status == AttendedJobStatus.Cancelled
                    ? current
                    : current with
                    {
                        Status = AttendedJobStatus.Completed,
                        CompletedUtc = _timeProvider.GetUtcNow(),
                        Result = result.Outputs
                    });
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            _statusStore.TryUpdate(
                job.RequestId,
                current => current with
                {
                    Status = AttendedJobStatus.Cancelled,
                    CompletedUtc = current.CompletedUtc ?? _timeProvider.GetUtcNow()
                });
        }
        catch (Exception exception)
        {
            _statusStore.TryUpdate(
                job.RequestId,
                current => current with
                {
                    Status = AttendedJobStatus.Failed,
                    CompletedUtc = _timeProvider.GetUtcNow(),
                    Error = "The attended automation failed."
                });

            _logger.LogError(
                "Attended job {RequestId} failed with {FailureType}.",
                job.RequestId,
                exception.GetType().Name);
        }
        finally
        {
            _activeJobs.TryRemove(job.RequestId, out _);
            cancellation.Dispose();
            _executionGate.Release();
        }
    }

    private void RemoveExpiredIdempotencyEntries(DateTimeOffset now)
    {
        foreach (var entry in _idempotencyEntries)
        {
            if (entry.Value.RetainUntilUtc <= now)
            {
                _idempotencyEntries.TryRemove(entry.Key, out _);
            }
        }
    }

    private static string ComputeRequestFingerprint(StartJobRequest request)
    {
        var canonical = new StringBuilder();
        AppendPart(canonical, request.ProcessAlias!);
        AppendPart(canonical, request.RequestedBy!);

        foreach (var input in request.Inputs!.OrderBy(
                     input => input.Key,
                     StringComparer.Ordinal))
        {
            AppendPart(canonical, input.Key);
            AppendPart(canonical, input.Value!);
        }

        return ComputeHash(canonical.ToString());
    }

    private static void AppendPart(StringBuilder builder, string value) =>
        builder.Append(value.Length).Append(':').Append(value).Append('|');

    private static string ComputeHash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static bool IsTerminal(AttendedJobStatus status) =>
        status is AttendedJobStatus.Completed
            or AttendedJobStatus.Failed
            or AttendedJobStatus.Cancelled;

    private sealed record IdempotencyEntry(
        Guid RequestId,
        string RequestFingerprint,
        DateTimeOffset RetainUntilUtc);
}
