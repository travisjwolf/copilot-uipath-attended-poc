using System.Text.Json.Serialization;

namespace AttendedLaunchBridge.Api.Domain;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AttendedJobStatus
{
    Pending,
    Running,
    Completed,
    Failed,
    Cancelled
}
