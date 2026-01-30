using LantanaGroup.Link.Census.Application.Interfaces;
using LantanaGroup.Link.Census.Application.Models.Enums;
using LantanaGroup.Link.Census.Application.Models.Payloads.Cerner;
using LantanaGroup.Link.Census.Application.Models.Payloads.Fhir.List;
using LantanaGroup.Link.Census.Domain.Entities.POI;

namespace LantanaGroup.Link.Census.Application.Factories;

public static class PatientEventFactory
{
    public static PatientEvent Create(string correlationId, string patientId, string visitId, string MRN, EventType eventType, IPayload payload, SourceType sourceType, string facilityId)
    {
        return new PatientEvent()
        {
            CorrelationId = correlationId,
            SourcePatientId = patientId,
            SourceVisitId = visitId,
            MedicalRecordNumber = MRN,
            EventType = eventType,
            Payload = payload,
            SourceType = sourceType,
            CreateDate = DateTime.Now,
            FacilityId = facilityId,
            EventDate = GetEventDate(payload)
        };
    }

    //TODO: Daniel - I don't think we need this. The event date should be the date that we wrote the entry into the table. The payload should contain any relevant date info
    private static DateTime GetEventDate(IPayload payload)
    {
        return payload switch
        {
            FHIRListDischargePayload dischargePayload => dischargePayload.DischargeDate,
            FHIRListAdmitPayload admitPayload => admitPayload.AdmitDate,
            CernerListAdmitPayload cernerAdmit => DateTime.UtcNow,
            CernerListDischargePayload cernerDischarge => DateTime.UtcNow,
            _ => throw new Exception("Unsupported payload type for event date extraction")
        };
    }
}
