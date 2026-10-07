using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Automation.UI.Services.Persistence;

public sealed class ApiHealthExecutionRunDocument
{
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    public Guid RunId { get; set; }

    [BsonRepresentation(BsonType.String)]
    public Guid? SeedRunId { get; set; }
    public string? SeedRunName { get; set; }

    public string Scope { get; set; } = string.Empty;
    public string? ServiceName { get; set; }

    [BsonRepresentation(BsonType.DateTime)]
    public DateTimeOffset StartedAt { get; set; }

    public string? Phase { get; set; }
    public string? Message { get; set; }
    public bool IsError { get; set; }
    public bool IsCompleted { get; set; }
    public bool Failed { get; set; }
    public string? Error { get; set; }

    [BsonRepresentation(BsonType.DateTime)]
    public DateTimeOffset? FinishedAt { get; set; }

    [BsonRepresentation(BsonType.DateTime)]
    public DateTimeOffset UpdatedAt { get; set; }
}
