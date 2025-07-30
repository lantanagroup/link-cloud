using LantanaGroup.Link.Census.Application.Factories;
using LantanaGroup.Link.Census.Application.Interfaces;
using LantanaGroup.Link.Census.Domain.Entities.POI;
using System.Text.Json;

namespace LantanaGroup.Link.Census.Application.Models.Payloads.Fhir.List;

public class FHIRListAdmitPayload : IPayload
{
    public string PatientId;
    public DateTime AdmitDate;

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
        var payloadStr = JsonSerializer.Serialize(this);

        return PatientEventFactory.Create(correlationId, PatientId, null, null, Enums.EventType.FHIRListAdmit, payloadStr, Enums.SourceType.FHIR, facilityId);
    }

    public PatientEncounter UpdatePatientEncounter(PatientEncounter patientEncounter)
    {
        throw new NotImplementedException();
    }
}
