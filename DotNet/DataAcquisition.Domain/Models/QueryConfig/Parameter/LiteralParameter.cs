using LantanaGroup.Link.DataAcquisition.Domain.Interfaces;

namespace LantanaGroup.Link.DataAcquisition.Domain.Models.QueryConfig.Parameter;

public class LiteralParameter : IParameter
{
    public string Name { get; set; }
    public string Literal { get; set; }
}
