using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Automation.UI.Services.Persistence;

[BsonIgnoreExtraElements]
public sealed class MeasureTemplateDocument
{
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsSystem { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public string GenerationFamily { get; set; } = string.Empty;
    public string BundleJson { get; set; } = string.Empty;

    public string? MeasureId { get; set; }
    public string? CanonicalUrl { get; set; }
    public string? Version { get; set; }
    public string? MeasureDate { get; set; }
    public string? Status { get; set; }
}
