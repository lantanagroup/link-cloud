using LantanaGroup.Link.Census.Application.Factories;
using LantanaGroup.Link.Census.Application.Interfaces;
using LantanaGroup.Link.Census.Application.Models.Enums;
using LantanaGroup.Link.Census.Domain.Entities.POI;
using System.Text.Json.Serialization;

namespace LantanaGroup.Link.Census.Application.Models.Payloads.Cerner
{
    public class CernerListAdmitPayload : IPayload
    {
        public string PayloadType { get; } = EventType.CernerListAdmit.ToString();
        public string PatientId { get; private set; }
        public DateTime AdmitDate { get; private set; }
        public string EncounterId { get; private set; }
        public string FinNumber { get; private set; }
        public string MedicalRecordNumber { get; private set; }
        public string EncounterStatus { get; private set; }
        public string EncounterType { get; private set; }

        public CernerListAdmitPayload(string patientId, DateTime admitDate, string encounterId, string finNumber, string medicalRecordNumber, string encounterStatus, string encounterType)
        {
            PatientId = patientId;
            AdmitDate = admitDate;
            EncounterId = encounterId;
            FinNumber = finNumber;
            MedicalRecordNumber = medicalRecordNumber;
            EncounterStatus = encounterStatus;
            EncounterType = encounterType;
        }

        public PatientEvent CreatePatientEvent(string facilityId, string correlationId)
        {
            return PatientEventFactory.Create(correlationId, PatientId, EncounterId, MedicalRecordNumber, Enums.EventType.CernerListAdmit, this, Enums.SourceType.SFTP_FHIR, facilityId);
        }

        public PatientEncounter CreatePatientEncounter(string facilityId, string correlationId)
        {
            return new PatientEncounterBuilder(facilityId, MedicalRecordNumber, AdmitDate, null, correlationId, EncounterType, EncounterStatus)
                .AddPatientIdentifier(PatientId, Enums.SourceType.SFTP_FHIR)
                .AddVisitIdentifier(EncounterId, SourceType.SFTP_FHIR)
                .GetPatientEncounter();
        }

        public PatientEncounter UpdatePatientEncounter(PatientEncounter patientEncounter)
        {
            throw new NotImplementedException();
        }
    }
}
