using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Interfaces;

namespace LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.QueryConfig.Parameter;

public class LiteralParameter : IParameter
{
    public ParameterType ParameterType { get; set; } = ParameterType.Literal;
    public string Name { get; set; }
    public string Literal { get; set; }
}
