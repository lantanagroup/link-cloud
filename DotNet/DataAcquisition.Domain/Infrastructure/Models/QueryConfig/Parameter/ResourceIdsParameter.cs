using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Interfaces;

namespace LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.QueryConfig.Parameter;

public class ResourceIdsParameter : IParameter
{
    public string Name { get; set; }
    public string Resource { get; set; }
    public string Paged { get; set; }
}
