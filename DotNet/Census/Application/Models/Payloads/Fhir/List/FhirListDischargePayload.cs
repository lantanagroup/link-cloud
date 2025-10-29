using LantanaGroup.Link.Census.Application.Interfaces;
using LantanaGroup.Link.Census.Application.Models.Enums;
using LantanaGroup.Link.Census.Models;
using System.Text.Json.Serialization;

namespace LantanaGroup.Link.Census.Application.Models.Payloads.Fhir.List;

public class FHIRListDischargePayload : IPayload
{
    public string? PayloadType { get; } = EventType.FHIRListDischarge.ToString();
    
    [JsonPropertyName("patientId")]
    public string PatientId;
    [JsonPropertyName("dischargeDate")]
    public DateTime DischargeDate;

    public FHIRListDischargePayload(string patientId, DateTime dischargeDate)
    {
        PatientId = patientId;
        DischargeDate = dischargeDate;
    }

    public PatientEncounterModel CreatePatientEncounter(string facilityId, string correlationId)
    {
        throw new NotImplementedException();
    }
    
    public PatientEventModel CreatePatientEvent(string facilityId, string correlationId)
    {
        return new PatientEventModel()
        {
            CorrelationId = correlationId,
            SourcePatientId = PatientId,
            SourceVisitId = null,
            MedicalRecordNumber = null,
            EventType = EventType.FHIRListDischarge,
            Payload = this,
            SourceType = SourceType.FHIR,
            FacilityId = facilityId
        };
    }

    public PatientEncounterModel UpdatePatientEncounter(PatientEncounterModel patientEncounter)
    {
        patientEncounter.ModifyDate = DateTime.Now;
        patientEncounter.DischargeDate = DischargeDate;

        return patientEncounter;
    }
}