using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.QueryConfig;
using MongoDB.Bson.Serialization.Attributes;
using System.Text.Json.Serialization;

namespace LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Interfaces;

[JsonDerivedType(typeof(ParameterQueryConfig), nameof(ParameterQueryConfig))]
[JsonDerivedType(typeof(ReferenceQueryConfig), nameof(ReferenceQueryConfig))]
[BsonKnownTypes(typeof(ParameterQueryConfig), typeof(ReferenceQueryConfig))]
public class IQueryConfig
{
    public IQueryConfig()
    {

    }
}
