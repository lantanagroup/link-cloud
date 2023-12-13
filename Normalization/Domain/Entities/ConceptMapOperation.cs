using LantanaGroup.Link.Normalization.Application.Serializers;
using MongoDB.Bson.Serialization.Attributes;

namespace LantanaGroup.Link.Normalization.Domain.Entities;

[BsonDiscriminator("ConceptMapOperation")]
public class ConceptMapOperation : INormalizationOperation
{
    public string FacilityId { get; set; }
    public string Name { get; set; }
    [BsonElement("FhirConceptMap")]
    [BsonSerializer(typeof(ConceptMapBsonSerializer))]
    public object? FhirConceptMap { get; set; }
    public string FhirPath { get; set; }
    public string FhirContext { get; set; }
    public DateTime CreateDate { get; set; }
    public DateTime ModifiedDate { get; set; }
}
