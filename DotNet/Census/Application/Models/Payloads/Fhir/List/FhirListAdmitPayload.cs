using System.ComponentModel;
using LantanaGroup.Link.Census.Application.Factories;
using LantanaGroup.Link.Census.Application.Interfaces;
using LantanaGroup.Link.Census.Domain.Entities.POI;
using System.Text.Json;
using System.Text.Json.Serialization;
using LantanaGroup.Link.Census.Application.Models.Enums;
using LantanaGroup.Link.Shared.Application.Models.DataAcq;

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

    public PatientEncounter CreatePatientEncounter(string facilityId, string correlationId)
    {
        return new PatientEncounterBuilder(facilityId, null, AdmitDate, null, correlationId)
                .AddPatientIdentifier(PatientId, Enums.SourceType.FHIR).GetPatientEncounter();
    }
    
    public PatientEvent CreatePatientEvent(string facilityId, string correlationId)
    {
        return PatientEventFactory.Create(correlationId, PatientId, null, null, Enums.EventType.FHIRListAdmit, this, Enums.SourceType.FHIR, facilityId);
    }

    public PatientEncounter UpdatePatientEncounter(PatientEncounter patientEncounter)
    {
        throw new NotImplementedException();
    }
}
