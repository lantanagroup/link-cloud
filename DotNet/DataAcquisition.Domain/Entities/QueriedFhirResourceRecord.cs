using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace LantanaGroup.Link.DataAcquisition.Domain.Entities;

[Table("queriedFhirResource")]
public class QueriedFhirResourceRecord : BaseEntity
{
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public string FacilityId { get; set; }
    public string? CorrelationId { get; set; }
    public string? PatientId { get; set; }
    public string? QueryType { get; set; }
    public string? ResourceType { get; set; }
    public string? ResourceId { get; set; }
    public bool IsSuccessful { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTime? CreateDate { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTime? ModifyDate { get; set; }
}
