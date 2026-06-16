using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Automation.UI.Services.Persistence;

/// <summary>MongoDB document for the api_health_runs collection.</summary>
[BsonIgnoreExtraElements]
public sealed class ApiHealthRunDocument
{
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    public Guid Id { get; set; }

    public string EndpointKey { get; set; } = string.Empty;
    public string ServiceName { get; set; } = string.Empty;
    public string EndpointName { get; set; } = string.Empty;

    public bool Passed { get; set; }
    public bool Skipped { get; set; }
    public string? SkipReason { get; set; }
    public int? ActualStatusCode { get; set; }
    public int ExpectedStatusCode { get; set; }
    public string? ErrorMessage { get; set; }
    public string? ResponseSnippet { get; set; }

    [BsonRepresentation(BsonType.DateTime)]
    public DateTimeOffset ExecutedAt { get; set; }

    public long DurationMs { get; set; }

    // Diagnostic fields persisted for post-refresh detail rendering.
    public string? RequestUrl { get; set; }
    public string? RequestMethod { get; set; }
    public string? RequestBody { get; set; }
    public string? TraceId { get; set; }
    public string? ResponseBody { get; set; }
}
