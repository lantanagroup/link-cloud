using LantanaGroup.Link.DataAcquisition.Domain.Entities;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Models;

public class FhirQueryResultModel
{
    public List<FhirQuery> Queries { get; set; }
}
