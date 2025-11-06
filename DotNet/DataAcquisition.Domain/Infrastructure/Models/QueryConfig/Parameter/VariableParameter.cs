using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Interfaces;

namespace LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.QueryConfig.Parameter;

public class VariableParameter : IParameter
{
    public ParameterType ParameterType { get; set; } = ParameterType.Variable;
    public string Name { get; set; }
    public Variable Variable { get; set; }
    public string? Format { get; set; }
}
