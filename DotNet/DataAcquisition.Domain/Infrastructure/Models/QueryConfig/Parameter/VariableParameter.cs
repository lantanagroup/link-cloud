using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Interfaces;
using MongoDB.Bson.Serialization.Attributes;

namespace LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.QueryConfig.Parameter;

public class VariableParameter : IParameter
{
    public string Name { get; set; }
    public Variable Variable { get; set; }
    [BsonIgnoreIfNull]
    public string? Format { get; set; }
}
