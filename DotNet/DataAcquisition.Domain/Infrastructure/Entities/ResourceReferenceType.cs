using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.Enums;
using LantanaGroup.Link.Shared.Domain.Entities;
using System.ComponentModel.DataAnnotations.Schema;

namespace LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;

[Table("ResourceReferenceType")]
public class ResourceReferenceType : BaseEntityExtended
{
    public string FacilityId { get; set; }
    public QueryPhase QueryPhase { get; set; }
    public string? ResourceType { get; set; }
    public string? FhirQueryId { get; set; }
    public FhirQuery? FhirQueryRef { get; set; }
}
