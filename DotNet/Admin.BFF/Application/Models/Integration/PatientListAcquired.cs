using Hl7.Fhir.Model;
using LantanaGroup.Link.LinkAdmin.BFF.Application.Interfaces.Models;
using LantanaGroup.Link.Shared.Application.Models.Kafka;

namespace LantanaGroup.Link.LinkAdmin.BFF.Application.Models.Integration
{
    public class PatientListAcquired : IPatientListsAcquired
    {
        /// <summary>
        /// Key for the patient event (FacilityId)
        /// </summary>
        /// <example>TestFacility01</example>
        public string FacilityId { get; set; } = string.Empty;

        /// <summary>
        /// The id of the patient subject to the event
        /// </summary>
        /// <example>TestPatient01</example>
        public List<PatientListItem> PatientLists { get; set; } = new List<PatientListItem>();

        public string ReportTrackingId { get; set; } = string.Empty;

    }


    public class PatientListAcquiredMessage
    {
        public List<PatientListItem> PatientListItems { get; set; } = new List<PatientListItem>();
        public string ReportTrackingId { get; set; } = string.Empty;
    }
}
