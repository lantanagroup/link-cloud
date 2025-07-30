using LantanaGroup.Link.Census.Domain.Entities.POI;

namespace LantanaGroup.Link.Census.Application.Interfaces;

public interface IPayload
{
    PatientEvent CreatePatientEvent(string facilityId, string correlationId);
    PatientEncounter CreatePatientEncounter(string facilityId, string correlationId);
    PatientEncounter UpdatePatientEncounter(PatientEncounter patientEncounter);
}