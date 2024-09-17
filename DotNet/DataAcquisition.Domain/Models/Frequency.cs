using LantanaGroup.Link.Shared.Application.Utilities;
using System.Text.Json.Serialization;

namespace LantanaGroup.Link.DataAcquisition.Domain.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum Frequency
{
    [StringValue("Discharge")]
    Discharge,
    [StringValue("Daily")]
    Daily,
    [StringValue("Weekly")]
    Weekly,
    [StringValue("Monthly")]
    Monthly
}
