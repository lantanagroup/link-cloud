using System.Text.Json.Serialization;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Api.Configuration;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum QueryPlanType
{
    QueryPlans, 
    Initial, 
    Supplemental
}


