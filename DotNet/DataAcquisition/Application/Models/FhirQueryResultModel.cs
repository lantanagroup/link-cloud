using LantanaGroup.Link.DataAcquisition.Domain.Entities;

namespace LantanaGroup.Link.DataAcquisition.Application.Models;

public class FhirQueryResultModel
{
    public List<FhirQuery> Queries { get; set; }
}
