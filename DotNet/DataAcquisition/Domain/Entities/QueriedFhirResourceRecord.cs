using LantanaGroup.Link.DataAcquisition.Domain.Models;
using LantanaGroup.Link.Shared.Domain.Attributes;
using LantanaGroup.Link.Shared.Domain.Entities;
using System.Text.Json.Serialization;

namespace LantanaGroup.Link.DataAcquisition.Domain.Entities;

[BsonCollection("queriedFhirResource")]
public class QueriedFhirResourceRecord : BaseEntity
{
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public string FacilityId { get; set; }
    public string CorrelationId { get; set; }
    public string PatientId { get; set; }
    public string QueryType { get; set; }
    public string ResourceType { get; set; }
    public string ResourceId { get; set; }
    public bool IsSuccessful { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTime? CreateDate { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTime? ModifyDate { get; set; }
}
