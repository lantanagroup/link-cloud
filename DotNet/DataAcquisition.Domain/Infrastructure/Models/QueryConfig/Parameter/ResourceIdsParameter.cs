using DataAcquisition.Domain.Infrastructure.Interfaces;

namespace DataAcquisition.Domain.Infrastructure.Models.QueryConfig.Parameter;

public class ResourceIdsParameter : IParameter
{
    public string Name { get; set; }
    public string Resource { get; set; }
    public string Paged { get; set; }
}
