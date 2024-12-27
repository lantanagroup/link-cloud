using LantanaGroup.Link.Shared.Application.Utilities;
using System.Text.Json.Serialization;

namespace LantanaGroup.Link.LinkAdmin.BFF.Application.Models.Integration
{

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum Frequency
    {
        [StringValue("Daily")]
        Daily = 1,
        [StringValue("Weekly")]
        Weekly = 2,
        [StringValue("Monthly")]
        Monthly = 3
    }
}
