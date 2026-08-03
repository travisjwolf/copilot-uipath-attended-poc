using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace AttendedLaunchBridge.Tests;

public sealed class BridgeWebApplicationFactory : WebApplicationFactory<Program>
{
    public const string ApiKey = "synthetic-test-api-key";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration(configuration =>
        {
            configuration.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["Bridge:ApiKey"] = ApiKey,
                    ["Bridge:RequestMaxAgeSeconds"] = "60",
                    ["Bridge:AllowedClockSkewSeconds"] = "1",
                    ["UiPath:UseFakeRobotClient"] = "true",
                    ["UiPath:FakeStartDelayMs"] = "75",
                    ["UiPath:FakeExecutionDelayMs"] = "250"
                });
        });
    }
}
