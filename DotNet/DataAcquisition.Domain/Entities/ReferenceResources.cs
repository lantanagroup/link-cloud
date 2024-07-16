using System.ComponentModel.DataAnnotations.Schema;
using LantanaGroup.Link.Shared.Domain.Entities;

namespace LantanaGroup.Link.DataAcquisition.Domain.Entities;

[Table("referenceResources")]
public class ReferenceResources : BaseEntityExtended
{
    public string FacilityId { get; set; }
    public string ResourceId { get; set; }
    public string ResourceType { get; set; }
    public string ReferenceResource { get; set; }
}
