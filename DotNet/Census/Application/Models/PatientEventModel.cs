using LantanaGroup.Link.Census.Application.Interfaces;
using LantanaGroup.Link.Census.Application.Models.Enums;
using LantanaGroup.Link.Census.Domain.Entities.POI;
using System.ComponentModel.DataAnnotations;

namespace LantanaGroup.Link.Census.Application.Models;

public class PatientEventModel
{
    public Guid Id { get; set; }
    [Required]
    public string FacilityId { get; set; }
    public string? CorrelationId { get; set; }
    [Required]
    public string SourcePatientId { get; set; }
    public string? SourceVisitId { get; set; }
    public string? MedicalRecordNumber { get; set; }
    public EventType EventType { get; set; }
    public IPayload Payload { get; set; }
    public SourceType SourceType { get; set; }
    public DateTime CreateDate { get; set; }
    public DateTime? ModifyDate { get; set; }


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
            EventType = this.EventType,
            Payload = this.Payload,
            SourceType = this.SourceType,
            CreateDate = this.CreateDate,
            ModifyDate = this.ModifyDate,
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
            EventType = patientEvent.EventType,
            Payload = patientEvent.Payload,
            SourceType = patientEvent.SourceType,
            CreateDate = patientEvent.CreateDate,
            ModifyDate = patientEvent.ModifyDate,
        };
    }
}
