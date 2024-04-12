using LantanaGroup.Link.Shared.Domain.Attributes;
using LantanaGroup.Link.Shared.Domain.Entities;

namespace LantanaGroup.Link.Normalization.Domain.Entities;

[BsonCollection("normalizationConfig")]
public class NormalizationConfigEntity : BaseEntity
{
    public string FacilityId { get; set; }
    public Dictionary<string, INormalizationOperation> OperationSequence { get; set; }
    public DateTime? CreatedDate { get; set; }
    public DateTime? ModifiedDate { get; set; }
}
