using LantanaGroup.Link.LinkAdmin.BFF.Application.Interfaces.Models;

namespace LantanaGroup.Link.LinkAdmin.BFF.Application.Models.Integration
{
    public class PatientEvent : IPatientEvent
    {
        /// <summary>
        /// Key for the patient event (FacilityId)
        /// </summary>
        public string Key { get; set; } = string.Empty;

        /// <summary>
        /// The id of the patient subject to the event
        /// </summary>
        public string PatientId { get; set; } = string.Empty;

        /// <summary>
        /// The type of event that occurred
        /// </summary>
        public string EventType { get; set; } = string.Empty;
    }

    public class PatientEventMessage
    {
        public string PatientId { get; set; } = string.Empty;
        public string EventType { get; set; } = string.Empty;
    }
}
