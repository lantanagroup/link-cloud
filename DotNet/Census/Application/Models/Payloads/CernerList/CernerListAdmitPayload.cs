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
        [JsonPropertyName("patientId")]
        public string PatientId;
        [JsonPropertyName("admitDate")]
        public DateTime AdmitDate;
        [JsonPropertyName("encounterId")]
        public string EncounterId;
        [JsonPropertyName("finNumber")]
        public string FinNumber;
        [JsonPropertyName("MedicalRecordNumber")]
        public string MedicalRecordNumber;
        [JsonPropertyName("EncounterStatus")]
        public string EncounterStatus;
        [JsonPropertyName("EncounterType")]
        public string EncounterType;

        [JsonConstructor]
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
            return PatientEventFactory.Create(correlationId, PatientId, EncounterId, MedicalRecordNumber, Enums.EventType.CernerListAdmit, this, Enums.SourceType.SFTP, facilityId);
        }

        public PatientEncounter CreatePatientEncounter(string facilityId, string correlationId)
        {
            throw new NotImplementedException();
        }

        public PatientEncounter UpdatePatientEncounter(PatientEncounter patientEncounter)
        {
            throw new NotImplementedException();
        }
    }
}
