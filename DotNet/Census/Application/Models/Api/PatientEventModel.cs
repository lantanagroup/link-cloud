using LantanaGroup.Link.Census.Application.Models.Enums;
using LantanaGroup.Link.Census.Domain.Entities.POI;
using System.ComponentModel.DataAnnotations;

namespace LantanaGroup.Link.Census.Application.Models.Api;

public class PatientEventModel
{
    public string Id { get; set; }
    [Required]
    public string FacilityId { get; set; }
    public string? CorrelationId { get; set; }
    [Required]
    public string SourcePatientId { get; set; }
    public string? SourceVisitId { get; set; }
    public string? MedicalRecordNumber { get; set; }
    public string EventType { get; set; }
    public string Payload { get; set; }
    public string SourceType { get; set; }

    public PatientEvent ToDomain()
    {
        return new PatientEvent
        {
            Id = this.Id,
            FacilityId = this.FacilityId,
            CorrelationId = this.CorrelationId,
            SourcePatientId = this.SourcePatientId,
            SourceVisitId = this.SourceVisitId,
            MedicalRecordNumber = this.MedicalRecordNumber,
            EventType = Enum.Parse<EventType>(this.EventType),
            Payload = this.Payload,
            SourceType = Enum.Parse<SourceType>(this.SourceType)
        };
    }

    public static PatientEventModel FromDomain(PatientEvent patientEvent)
    {
        return new PatientEventModel
        {
            Id = patientEvent.Id,
            FacilityId = patientEvent.FacilityId,
            CorrelationId = patientEvent.CorrelationId,
            SourcePatientId = patientEvent.SourcePatientId,
            SourceVisitId = patientEvent.SourceVisitId,
            MedicalRecordNumber = patientEvent.MedicalRecordNumber,
            EventType = patientEvent.EventType.ToString(),
            Payload = patientEvent.Payload,
            SourceType = patientEvent.SourceType.ToString()
        };
    }
}
