using MongoDB.Bson.Serialization.Attributes;
using System.Text.Json.Serialization;

namespace LantanaGroup.Link.DataAcquisition.Domain.Models;

public class AuthenticationConfiguration
{
    [BsonIgnoreIfNull]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string AuthType { get; set; }
    
    [BsonIgnoreIfNull]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string Key { get; set; }

    [BsonIgnoreIfNull]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string TokenUrl { get; set; }

    [BsonIgnoreIfNull]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string Audience { get; set; }

    [BsonIgnoreIfNull]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string ClientId { get; set; }
    
    [BsonIgnoreIfNull]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? UserName { get; set; }
    
    [BsonIgnoreIfNull]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Password { get; set; }
}
