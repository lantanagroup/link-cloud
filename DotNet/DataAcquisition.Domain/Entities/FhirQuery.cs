using DataAcquisition.Domain.Entities;
using DataAcquisition.Domain.Models.Enums;
using Hl7.Fhir.Model;
using LantanaGroup.Link.Shared.Domain.Entities;
using System.ComponentModel.DataAnnotations.Schema;

namespace LantanaGroup.Link.DataAcquisition.Domain.Entities;

[Table("FhirQuery")]
public class FhirQuery : BaseEntityExtended
{
    public string FacilityId { get; set; }
    public FhirQueryType QueryType { get; set; }
    public List<ResourceType> ResourceTypes { get; set; }
    public List<string> QueryParameters { get; set; } = new List<string>();
    public List<ResourceReferenceTypeEntity> ResourceReferenceType { get; set; }
    public int? Paged { get; set; }
}
