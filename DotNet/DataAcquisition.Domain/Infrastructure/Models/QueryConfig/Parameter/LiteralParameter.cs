using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Interfaces;

namespace LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.QueryConfig.Parameter;

public class LiteralParameter : IParameter
{
    public string Name { get; set; } = string.Empty;
    public string Literal { get; set; } = string.Empty;
}
