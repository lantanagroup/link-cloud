using System.Text.Json.Serialization;

namespace LantanaGroup.Link.Shared.Application.Enums
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum ScheduleStatus
    {
        New = 0,
        Scheduled = 100,
        EndOfPeriod = 200,
        Submitted = 500
    }
}
