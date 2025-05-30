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
}
