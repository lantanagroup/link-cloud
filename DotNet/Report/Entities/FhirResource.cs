using Hl7.Fhir.Model;
using LantanaGroup.Link.Report.Domain.Enums;
using LantanaGroup.Link.Shared.Domain.Attributes;
using System.Text.Json.Serialization;

namespace LantanaGroup.Link.Report.Entities;

[BsonCollection("fhirResource")]
public class FhirResource
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public DateTime CreateDate { get; set; } = DateTime.UtcNow;
    public DateTime? ModifyDate { get; set; }

    public ResourceCategoryType ResourceCategoryType { get; set; }
    public string? PatientId { get; set; }
    public string FacilityId { get; set; } = string.Empty;
    public string ResourceType { get; set; } = string.Empty;
    public string ResourceId { get; set; } = string.Empty;

    public Resource Resource { get; set; } = null!;
}