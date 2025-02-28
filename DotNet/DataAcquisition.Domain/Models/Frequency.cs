using LantanaGroup.Link.Shared.Application.Utilities;
using System.Text.Json.Serialization;

namespace LantanaGroup.Link.DataAcquisition.Domain.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum Frequency
{
    [StringValue("Discharge")]
    Discharge = 0,
    [StringValue("Daily")]
    Daily = 1,
    [StringValue("Weekly")]
    Weekly = 2,
    [StringValue("Monthly")]
    Monthly = 3,
    [StringValue("Adhoc")]
    Adhoc = 4
}
