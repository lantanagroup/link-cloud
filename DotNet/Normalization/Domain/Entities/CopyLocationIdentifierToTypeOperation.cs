using MongoDB.Bson.Serialization.Attributes;

namespace LantanaGroup.Link.Normalization.Domain.Entities;

[BsonDiscriminator("CopyLocationIdentifierToTypeOperation")]
public class CopyLocationIdentifierToTypeOperation : INormalizationOperation
{
    public string Name { get; set; }
}
