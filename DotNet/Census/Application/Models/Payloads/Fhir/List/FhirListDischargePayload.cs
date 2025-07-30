using LantanaGroup.Link.Census.Application.Factories;
using LantanaGroup.Link.Census.Application.Interfaces;
using LantanaGroup.Link.Census.Domain.Entities.POI;
using System.Text.Json;

namespace LantanaGroup.Link.Census.Application.Models.Payloads.Fhir.List;

public class FHIRListDischargePayload : IPayload
{
    public string PatientId;
    public DateTime DischargeDate;

    public FHIRListDischargePayload(string patientId, DateTime dischargeDate)
    {
        PatientId = patientId;
        DischargeDate = dischargeDate;
    }

    public PatientEncounter CreatePatientEncounter(string facilityId, string correlationId)
    {
        throw new NotImplementedException();
    }

    public PatientEvent CreatePatientEvent(string facilityId, string correlationId)
    {
        var payload = JsonSerializer.Serialize(this);

        return PatientEventFactory.Create(correlationId, PatientId, null, null, Enums.EventType.FHIRListDischarge, payload, Enums.SourceType.FHIR, facilityId);
    }

    public PatientEncounter UpdatePatientEncounter(PatientEncounter patientEncounter)
    {
        patientEncounter.ModifyDate = DateTime.Now;
        patientEncounter.DischargeDate = DischargeDate;

        return patientEncounter;
    }

    public string GetCorrelationId()
    {
        return null;
    }
}