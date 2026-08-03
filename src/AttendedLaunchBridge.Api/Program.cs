using System.Text.Json.Serialization;
using AttendedLaunchBridge.Api.Application.Abstractions;
using AttendedLaunchBridge.Api.Application.Services;
using AttendedLaunchBridge.Api.Endpoints;
using AttendedLaunchBridge.Api.Infrastructure;
using AttendedLaunchBridge.Api.Infrastructure.Fake;
using AttendedLaunchBridge.Api.Infrastructure.UiPath;
using AttendedLaunchBridge.Api.Middleware;
using AttendedLaunchBridge.Api.Options;

var builder = WebApplication.CreateBuilder(args);

var environmentApiKey = Environment.GetEnvironmentVariable(
    BridgeSecurityOptions.ApiKeyEnvironmentVariable);
if (!string.IsNullOrWhiteSpace(environmentApiKey))
{
    builder.Configuration.AddInMemoryCollection(
        new Dictionary<string, string?>
        {
            ["Bridge:ApiKey"] = environmentApiKey
        });
}

builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = builder.Configuration.GetValue<long?>(
        "Bridge:MaxRequestBodyBytes") ?? 16_384;
});

builder.Services
    .AddOptions<UiPathOptions>()
    .BindConfiguration(UiPathOptions.SectionName)
    .Validate(
        options => options.AllowedProcesses.Count > 0,
        "At least one UiPath process alias must be configured.")
    .ValidateOnStart();

builder.Services
    .AddOptions<BridgeSecurityOptions>()
    .BindConfiguration(BridgeSecurityOptions.SectionName)
    .Validate(
        options => !string.IsNullOrWhiteSpace(options.ApiKey),
        $"Set Bridge:ApiKey with user secrets or the {BridgeSecurityOptions.ApiKeyEnvironmentVariable} environment variable.")
    .Validate(
        options => options.RequestMaxAgeSeconds > 0 &&
                   options.AllowedClockSkewSeconds >= 0 &&
                   options.MaxRequestBodyBytes > 0 &&
                   options.MaxInputCount > 0 &&
                   options.MaxInputNameLength > 0 &&
                   options.MaxInputValueLength > 0 &&
                   options.MaxTotalInputCharacters > 0 &&
                   options.MaxRequestedByLength > 0 &&
                   options.MaxProcessAliasLength > 0 &&
                   options.MaxIdempotencyKeyLength > 0 &&
                   options.MaxConcurrentJobs > 0 &&
                   options.IdempotencyRetentionMinutes > 0,
        "Bridge security limits must be positive.")
    .ValidateOnStart();

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
    options.SerializerOptions.UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow;
});
builder.Services.AddProblemDetails();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<ApiKeyEndpointFilter>();
builder.Services.AddSingleton<RequestSizeEndpointFilter>();
builder.Services.AddSingleton<IJobStatusStore, InMemoryJobStatusStore>();
builder.Services.AddSingleton<IProcessAuthorizationService, ConfigurationProcessAuthorizationService>();

if (builder.Configuration.GetValue<bool>("UiPath:UseFakeRobotClient"))
{
    builder.Services.AddSingleton<FakeRobotClient>();
    builder.Services.AddSingleton<IRobotProcessProvider>(
        services => services.GetRequiredService<FakeRobotClient>());
    builder.Services.AddSingleton<IAttendedJobLauncher>(
        services => services.GetRequiredService<FakeRobotClient>());
}
else
{
    builder.Services.AddSingleton<UiPathRobotClient>();
    builder.Services.AddSingleton<IRobotProcessProvider>(
        services => services.GetRequiredService<UiPathRobotClient>());
    builder.Services.AddSingleton<IAttendedJobLauncher>(
        services => services.GetRequiredService<UiPathRobotClient>());
}

builder.Services.AddSingleton<AttendedJobService>();

var app = builder.Build();

app.UseExceptionHandler();
app.UseMiddleware<CorrelationIdMiddleware>();
app.MapBridgeEndpoints();
app.Run();

public partial class Program;
