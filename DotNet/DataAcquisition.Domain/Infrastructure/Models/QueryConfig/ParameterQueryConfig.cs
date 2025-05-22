using DataAcquisition.Domain.Infrastructure.Interfaces;
using MongoDB.Bson.Serialization.Attributes;

namespace DataAcquisition.Domain.Infrastructure.Models.QueryConfig;

public class ParameterQueryConfig : IQueryConfig
{
    public string ResourceType { get; set; }
    public List<IParameter> Parameters { get; set; }

    public ParameterQueryConfig()
    {

    }
}
