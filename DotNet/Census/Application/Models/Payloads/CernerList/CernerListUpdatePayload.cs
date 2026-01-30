using LantanaGroup.Link.Census.Application.Factories;
using LantanaGroup.Link.Census.Application.Interfaces;
using LantanaGroup.Link.Census.Application.Models.Enums;
using LantanaGroup.Link.Census.Domain.Entities.POI;

namespace LantanaGroup.Link.Census.Application.Models.Payloads.CernerList
{
    public class CernerListUpdatePayload : IPayload
    {
        public string PayloadType { get; } = EventType.CernerListAdmit.ToString();
        public string PatientId { get; private set; }
        public string EncounterId { get; private set; }
        public string FinNumber { get; private set; }
        public string MedicalRecordNumber { get; private set; }
        public string EncounterStatus { get; private set; }
        public string EncounterType { get; private set; }

        public CernerListUpdatePayload(string patientId, string encounterId, string finNumber, string medicalRecordNumber, string encounterStatus, string encounterType)
        {
            PatientId = patientId;
            EncounterId = encounterId;
            FinNumber = finNumber;
            MedicalRecordNumber = medicalRecordNumber;
            EncounterStatus = encounterStatus;
            EncounterType = encounterType;
        }

        public PatientEvent CreatePatientEvent(string facilityId, string correlationId)
        {
            return PatientEventFactory.Create(correlationId, PatientId, EncounterId, MedicalRecordNumber, Enums.EventType.CernerListUpdate, this, Enums.SourceType.SFTP, facilityId);
        }

        public PatientEncounter CreatePatientEncounter(string facilityId, string correlationId)
        {
            throw new NotImplementedException();
        }

        public PatientEncounter UpdatePatientEncounter(PatientEncounter patientEncounter)
        {
            patientEncounter.EncounterStatus = EncounterStatus;
            patientEncounter.EncounterType = EncounterType;
            patientEncounter.MedicalRecordNumber = MedicalRecordNumber;

            return patientEncounter;
        }
    }
}
