using LantanaGroup.Link.LinkAdmin.BFF.Application.Interfaces.Models;

namespace LantanaGroup.Link.LinkAdmin.BFF.Application.Models.Integration
{
    public class PatientEvent : IPatientEvent
    {
        /// <summary>
        /// Key for the patient event (FacilityId)
        /// </summary>
        /// <example>TestFacility01</example>
        public string Key { get; set; } = string.Empty;

        /// <summary>
        /// The id of the patient subject to the event
        /// </summary>
        /// <example>TestPatient01</example>
        public string PatientId { get; set; } = string.Empty;

        /// <summary>
        /// The type of event that occurred
        /// </summary>
        /// <example>Discharge</example>
        public string EventType { get; set; } = string.Empty;
    }

    public class PatientEventMessage
    {
        public string PatientId { get; set; } = string.Empty;
        public string EventType { get; set; } = string.Empty;
    }
}
