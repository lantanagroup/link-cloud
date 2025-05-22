using DataAcquisition.Domain.Infrastructure.Interfaces;

namespace DataAcquisition.Domain.Infrastructure.Models.QueryConfig.Parameter;

public class LiteralParameter : IParameter
{
    public string Name { get; set; }
    public string Literal { get; set; }
}
