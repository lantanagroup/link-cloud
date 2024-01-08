using MongoDB.Bson.Serialization.Attributes;

namespace LantanaGroup.Link.Normalization.Domain.Entities;

[BsonDiscriminator("CopyElementOperation")]
public class CopyElementOperation : INormalizationOperation
{
    public string FacilityId { get; set; }
    public string Name { get; set; }
    public string FromFhirPath { get; set; }
    public string ToFhirPath { get; set; }
    public DateTime CreateDate { get; set; }
    public DateTime ModifiedDate { get; set; }
}
