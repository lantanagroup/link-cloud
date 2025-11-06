using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Interfaces;

namespace LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.QueryConfig;
public class ParameterQueryConfig : IQueryConfig
{
    public QueryConfigType QueryConfigType { get; set; } = QueryConfigType.Parameter;
    public string ResourceType { get; set; }
    public List<IParameter> Parameters { get; set; }
    
    public ParameterQueryConfig()
    {

    }
}
