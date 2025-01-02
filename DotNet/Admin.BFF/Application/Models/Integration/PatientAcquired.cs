using Hl7.Fhir.Model;
using LantanaGroup.Link.LinkAdmin.BFF.Application.Interfaces.Models;

namespace LantanaGroup.Link.LinkAdmin.BFF.Application.Models.Integration
{
    public class PatientAcquired : IPatientsAcquired
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
        public List<string> PatientIds { get; set; } = [];

    }


    public class PatientAcquiredMessage
    {
        public List PatientIds { get; set; }
    }
}
