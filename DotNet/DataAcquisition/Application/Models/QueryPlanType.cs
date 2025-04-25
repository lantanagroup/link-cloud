using System.Text.Json.Serialization;

namespace LantanaGroup.Link.DataAcquisition.Application.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum QueryPlanType
{
    QueryPlans, 
    Initial, 
    Supplemental
}
