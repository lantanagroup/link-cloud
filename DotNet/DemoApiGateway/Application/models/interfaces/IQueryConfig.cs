using LantanaGroup.Link.DemoApiGateway.Application.models.dataAcquisition;
using MongoDB.Bson.Serialization.Attributes;
using System.Text.Json.Serialization;

namespace LantanaGroup.Link.DemoApiGateway.Application.models.interfaces
{
    [JsonDerivedType(typeof(ParameterQueryConfig), nameof(ParameterQueryConfig))]
    [JsonDerivedType(typeof(ReferenceQueryConfig), nameof(ReferenceQueryConfig))]
    public abstract class IQueryConfig
    {
    }   
}
