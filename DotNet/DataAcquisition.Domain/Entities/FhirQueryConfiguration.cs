using LantanaGroup.Link.DataAcquisition.Domain.Models;
using LantanaGroup.Link.Shared.Domain.Attributes;
using System.ComponentModel.DataAnnotations.Schema;
using LantanaGroup.Link.Shared.Domain.Entities;
using MongoDB.Bson.Serialization.Attributes;
using System.Text.Json.Serialization;
using System.Runtime.Serialization;

namespace LantanaGroup.Link.DataAcquisition.Domain.Entities;

[DataContract]
[Table("fhirQueryConfiguration")]
public class FhirQueryConfiguration : BaseEntityExtended
{
    [DataMember]
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public string FacilityId { get; set; }

    [DataMember]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string FhirServerBaseUrl { get; set; }

    [DataMember]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [BsonIgnoreIfNull]
    public AuthenticationConfiguration? Authentication { get; set; }

    [DataMember]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int MaxConcurrentRequests { get; set; } = 8;

    [DataMember]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public TimeSpan MinAcquisitionPullTime { get; set; } = default;

    [DataMember]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public TimeSpan MaxAcquisitionPullTime { get; set; } = default;
}
