using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Interfaces;

namespace LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.QueryConfig;

public class ParameterQueryConfig : IQueryConfig
{
    public string ResourceType { get; set; } = string.Empty;
    public List<IParameter> Parameters { get; set; } = new();

    public ParameterQueryConfig()
    {

    }
}
