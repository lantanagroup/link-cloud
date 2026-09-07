using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using Automation.UI.Models.ApiHealth;

namespace Automation.UI.Services.Persistence;

/// <summary>MongoDB document for the api_health_runs collection.</summary>
[BsonIgnoreExtraElements]
public sealed class ApiHealthRunDocument
{
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    public Guid Id { get; set; }

    [BsonRepresentation(BsonType.String)]
    public Guid RunId { get; set; }

    public string ServiceName { get; set; } = string.Empty;
    public string RunMode { get; set; } = "Single";

    [BsonRepresentation(BsonType.DateTime)]
    public DateTimeOffset StartedAt { get; set; }

    /// <summary>
    /// Legacy embedded endpoint results retained for compatibility with existing API Health history.
    /// New results are persisted separately.
    /// </summary>
    public List<ApiTestRunResult> EndpointResults { get; set; } = [];
}