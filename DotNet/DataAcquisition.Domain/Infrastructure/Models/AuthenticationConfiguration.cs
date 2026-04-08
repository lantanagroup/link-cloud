using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models;

[DataContract]
public class AuthenticationConfiguration
{
    [DataMember]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? AuthType { get; set; }

    [DataMember]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Key { get; set; }

    [DataMember]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? TokenUrl { get; set; }

    [DataMember]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Audience { get; set; }

    [DataMember]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ClientId { get; set; }

    [DataMember]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? UserName { get; set; }

    [DataMember]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Password { get; set; }

    [DataMember]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, string>? CustomHeaders { get; set; }
}
