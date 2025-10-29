using LantanaGroup.Link.Census.Application.Factories;
using LantanaGroup.Link.Census.Application.Interfaces;
using LantanaGroup.Link.Census.Application.Models.Enums;
using LantanaGroup.Link.Census.Models;
using System.Text.Json.Serialization;

namespace LantanaGroup.Link.Census.Application.Models.Payloads.Fhir.List;

public class FHIRListAdmitPayload : IPayload
{
    public string PayloadType { get; } = EventType.FHIRListAdmit.ToString();
    
    [JsonPropertyName("patientId")]
    public string PatientId;
    [JsonPropertyName("admitDate")]
    public DateTime AdmitDate;

    [JsonConstructor]
    public FHIRListAdmitPayload(string patientId, DateTime admitDate)
    {
        PatientId = patientId;
        AdmitDate = admitDate;
    }

    public PatientEncounterModel CreatePatientEncounter(string facilityId, string correlationId)
    {
        return new PatientEncounterBuilder(facilityId, null, AdmitDate, null, correlationId)
                .AddPatientIdentifier(PatientId, Enums.SourceType.FHIR).GetPatientEncounter();
    }
    
    public PatientEventModel CreatePatientEvent(string facilityId, string correlationId)
    {
        return new PatientEventModel()
        {
            CorrelationId = correlationId,
            SourcePatientId = PatientId,
            SourceVisitId = null,
            MedicalRecordNumber = null,
            EventType = EventType.FHIRListAdmit,
            Payload = this,
            SourceType = SourceType.FHIR,
            FacilityId = facilityId
        };
    }

    public PatientEncounterModel UpdatePatientEncounter(PatientEncounterModel patientEncounter)
    {
        throw new NotImplementedException();
    }
}
