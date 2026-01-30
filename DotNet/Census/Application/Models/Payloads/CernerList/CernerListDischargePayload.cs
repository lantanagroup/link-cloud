using LantanaGroup.Link.Census.Application.Factories;
using LantanaGroup.Link.Census.Application.Interfaces;
using LantanaGroup.Link.Census.Application.Models.Enums;
using LantanaGroup.Link.Census.Domain.Entities.POI;

namespace LantanaGroup.Link.Census.Application.Models.Payloads.Cerner
{
    public class CernerListDischargePayload : IPayload
    {
        public string PayloadType { get; } = EventType.CernerListDischarge.ToString();
        public string PatientId { get; private set; }
        public DateTime DischargeDate { get; private set; }

        public CernerListDischargePayload(string patientId, DateTime dischargeDate)
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
            return PatientEventFactory.Create(correlationId, PatientId, null, null, Enums.EventType.CernerListDischarge, this, Enums.SourceType.SFTP, facilityId);
        }

        public PatientEncounter UpdatePatientEncounter(PatientEncounter patientEncounter)
        {
            throw new NotImplementedException();
        }
    }
}
