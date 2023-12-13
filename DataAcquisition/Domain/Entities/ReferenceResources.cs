using LantanaGroup.Link.Shared.Domain.Attributes;
using LantanaGroup.Link.Shared.Domain.Entities;

namespace LantanaGroup.Link.DataAcquisition.Domain.Entities;

[BsonCollection("referenceResources")]
public class ReferenceResources : BaseEntity
{
    public string FacilityId { get; set; }
    public string ResourceId { get; set; }
    public string ResourceType { get; set; }
    public string ReferenceResource { get; set; }
    public DateTime CreateDate { get; set; }
    public DateTime ModifyDate { get; set; }
}
