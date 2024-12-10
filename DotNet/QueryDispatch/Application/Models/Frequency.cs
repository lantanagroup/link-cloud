using LantanaGroup.Link.Shared.Application.Utilities;
using System.Text.Json.Serialization;

namespace QueryDispatch.Application.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum Frequency
{
    [StringValue("Daily")]
    Daily,
    [StringValue("Weekly")]
    Weekly,
    [StringValue("Monthly")]
    Monthly
}
