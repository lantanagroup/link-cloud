using MongoDB.Bson.Serialization.Attributes;

namespace LantanaGroup.Link.Normalization.Domain.Entities;

[BsonDiscriminator("PeriodDateFixerOperation")]
public class PeriodDateFixerOperation : INormalizationOperation
{
    public string Name { get; set; }
}
