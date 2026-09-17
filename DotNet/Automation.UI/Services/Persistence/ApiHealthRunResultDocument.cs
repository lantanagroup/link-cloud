using Automation.UI.Models.ApiHealth;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Automation.UI.Services.Persistence;

/// <summary>
/// MongoDB document for an individual API Health endpoint result.
/// </summary>
[BsonIgnoreExtraElements]
public sealed class ApiHealthRunResultDocument
{
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    public Guid Id { get; set; }

    [BsonRepresentation(BsonType.String)]
    public Guid RunId { get; set; }

    public string ServiceName { get; set; } = string.Empty;

    public string EndpointKey { get; set; } = string.Empty;

    [BsonRepresentation(BsonType.DateTime)]
    public DateTimeOffset StartedAt { get; set; }

    public ApiTestRunResult Result { get; set; } = new();
}