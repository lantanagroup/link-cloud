using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Automation.UI.Services.Persistence;

public sealed class GenerationCatalogDocument
{
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    public Guid Id { get; set; }
    public string Kind { get; set; } = "";
    public string System { get; set; } = "";
    public string Code { get; set; } = "";
    public string Display { get; set; } = "";
    public bool Incomplete { get; set; }
    public bool IsSeed { get; set; }
    public string? SourceValueSet { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public string PayloadJson { get; set; } = "{}";
}
