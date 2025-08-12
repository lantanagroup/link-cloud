using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.QueryConfig.Parameter;
using MongoDB.Bson.Serialization.Attributes;
using System.Text.Json.Serialization;

namespace LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Interfaces;

[JsonDerivedType(typeof(LiteralParameter), nameof(LiteralParameter))]
[JsonDerivedType(typeof(ResourceIdsParameter), nameof(ResourceIdsParameter))]
[JsonDerivedType(typeof(VariableParameter), nameof(VariableParameter))]
[BsonKnownTypes(typeof(LiteralParameter), typeof(ResourceIdsParameter), typeof(VariableParameter))]
public abstract class IParameter
{
}
