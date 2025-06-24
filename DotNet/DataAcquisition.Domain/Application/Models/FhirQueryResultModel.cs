using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Models;

public class FhirQueryResultModel
{
    public List<FhirQuery> Queries { get; set; }
}
