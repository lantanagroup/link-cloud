using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.Enums;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;

[Table("ResourceReferenceType")]
public class ResourceReferenceType
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string FacilityId { get; set; }
    public QueryPhase QueryPhase { get; set; }
    public string? ResourceType { get; set; }
    public string? FhirQueryId { get; set; }
    public FhirQuery? FhirQueryRef { get; set; }
    public DateTime CreateDate { get; set; } = DateTime.UtcNow;
    public DateTime? ModifyDate { get; set; }

}
