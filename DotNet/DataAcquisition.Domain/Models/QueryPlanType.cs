using LantanaGroup.Link.Shared.Application.Utilities;
using System.Text.Json.Serialization;

namespace DataAcquisition.Domain.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum QueryPlanType
{
    [StringValue("Monthly")]
    Monthly,
    [StringValue("Weekly")]
    Weekly,
    [StringValue("Daily")]
    Daily,
    [StringValue("Discharge")]
    Discharge
}
