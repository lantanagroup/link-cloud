using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Automation.UI.Services.Persistence;

/// <summary>
/// MongoDB document for <see cref="Models.QueryPlanTemplate"/>.
/// </summary>
public class QueryPlanTemplateDocument
{
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsSystem { get; set; }
    public bool IsDefault { get; set; }
    public string EhrDescription { get; set; } = "Epic";
    public string LookBack { get; set; } = "P0D";
    public string InitialQueriesJson { get; set; } = "[]";
    public string SupplementalQueriesJson { get; set; } = "[]";
    public DateTimeOffset UpdatedAt { get; set; }
}
