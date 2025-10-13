using LantanaGroup.Link.Census.Domain.Entities.POI;

namespace LantanaGroup.Link.Census.Application.Interfaces;

public interface IPayload
{
    string PayloadType { get; }
    PatientEvent CreatePatientEvent(string facilityId, string correlationId);
    PatientEncounter CreatePatientEncounter(string facilityId, string correlationId);
    PatientEncounter UpdatePatientEncounter(PatientEncounter patientEncounter);
}