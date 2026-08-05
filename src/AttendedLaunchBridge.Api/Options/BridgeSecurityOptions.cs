namespace AttendedLaunchBridge.Api.Options;

public sealed class BridgeSecurityOptions
{
    public const string SectionName = "Bridge";
    public const string ApiKeyEnvironmentVariable = "COPILOT_UIPATH_BRIDGE_API_KEY";
    public const string DefaultApiKeyHeaderName = "X-Api-Key";
    public const string DefaultIdempotencyKeyHeaderName = "Idempotency-Key";
    public const string DefaultRequestTimestampHeaderName = "X-Request-Timestamp";
    public const string DefaultCorrelationIdHeaderName = "X-Correlation-ID";

    public string? ApiKey { get; init; }

    public string ApiKeyHeaderName { get; init; } = DefaultApiKeyHeaderName;

    public bool EnableGatewayBasicAuthentication { get; init; } = true;

    public string GatewayBasicUsername { get; init; } = "CopilotGateway";

    public string IdempotencyKeyHeaderName { get; init; } = DefaultIdempotencyKeyHeaderName;

    public string RequestTimestampHeaderName { get; init; } = DefaultRequestTimestampHeaderName;

    public string CorrelationIdHeaderName { get; init; } = DefaultCorrelationIdHeaderName;

    public int RequestMaxAgeSeconds { get; init; } = 300;

    public int AllowedClockSkewSeconds { get; init; } = 30;

    public long MaxRequestBodyBytes { get; init; } = 16_384;

    public int MaxInputCount { get; init; } = 10;

    public int MaxInputNameLength { get; init; } = 100;

    public int MaxInputValueLength { get; init; } = 4_096;

    public int MaxTotalInputCharacters { get; init; } = 8_192;

    public int MaxRequestedByLength { get; init; } = 128;

    public int MaxProcessAliasLength { get; init; } = 100;

    public int MaxIdempotencyKeyLength { get; init; } = 200;

    public int MaxConcurrentJobs { get; init; } = 1;

    public int IdempotencyRetentionMinutes { get; init; } = 1_440;
}
