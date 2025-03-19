using LantanaGroup.Link.Shared.Domain.Entities;
using System.ComponentModel.DataAnnotations.Schema;

namespace LantanaGroup.Link.DataAcquisition.Domain.Entities;

[Table("fhirQuery")]
public class FhirQuery : BaseEntityExtended
{
    public string FacilityId { get; set; }
    public string? CorrelationId { get; set; }
    public string? PatientId { get; set; }
    public string ResourceType { get; set; }
    public string? SearchParams { get; set; }
    public string? RequestBody { get; set; }
}
