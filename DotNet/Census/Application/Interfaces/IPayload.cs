using LantanaGroup.Link.Census.Application.Models;
using LantanaGroup.Link.Census.Domain.Entities.POI;
using LantanaGroup.Link.Census.Models;

namespace LantanaGroup.Link.Census.Application.Interfaces;

public interface IPayload
{
    string PayloadType { get; }
    PatientEventModel CreatePatientEvent(string facilityId, string correlationId);
    PatientEncounterModel CreatePatientEncounter(string facilityId, string correlationId);
    PatientEncounterModel UpdatePatientEncounter(PatientEncounterModel patientEncounter);
}