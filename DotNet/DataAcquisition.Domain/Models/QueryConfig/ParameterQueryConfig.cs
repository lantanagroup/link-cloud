using LantanaGroup.Link.DataAcquisition.Domain.Interfaces;
using MongoDB.Bson.Serialization.Attributes;

namespace LantanaGroup.Link.DataAcquisition.Domain.Models.QueryConfig;

public class ParameterQueryConfig : IQueryConfig
{
    public string ResourceType { get; set; }
    public List<IParameter> Parameters { get; set; }

    public ParameterQueryConfig()
    {

    }
}
