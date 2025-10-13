using LantanaGroup.Link.Census.Application.Interfaces;
using LantanaGroup.Link.Census.Application.Models.Enums;
using LantanaGroup.Link.Census.Application.Models.Payloads.Fhir.List;
using LantanaGroup.Link.Shared.Domain.Entities;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LantanaGroup.Link.Census.Domain.Entities.POI;

[Table("PatientEvents")]
public class PatientEvent : BaseEntityExtended
{
    [Required]
    public string FacilityId { get; set; }
    public string? CorrelationId { get; set; }
    [Required]
    public string SourcePatientId { get; set; }
    public string? SourceVisitId { get; set; }
    public string? MedicalRecordNumber { get; set; }
    [Column(TypeName = "nvarchar(255)")]
    public EventType EventType { get; set; }
    [JsonConverter(typeof(PayloadJsonConverter))]
    public IPayload Payload { get; set; }
    [Column(TypeName = "nvarchar(255)")]
    public SourceType SourceType { get; set; }
}
