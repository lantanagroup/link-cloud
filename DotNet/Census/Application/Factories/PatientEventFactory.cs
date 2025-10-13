using LantanaGroup.Link.Census.Application.Interfaces;
using LantanaGroup.Link.Census.Application.Models.Enums;
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
            FacilityId = facilityId
        };
    }
}
