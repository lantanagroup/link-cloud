using LantanaGroup.Link.LinkAdmin.BFF.Application.Interfaces.Models;

namespace LantanaGroup.Link.LinkAdmin.BFF.Application.Models.Integration
{
    public class Facility
    {
        /// <summary>
        /// Key for the patient event (FacilityId)
        /// </summary>
        /// <example>TestFacility01</example>
        public string FacilityId { get; set; } = string.Empty;

    }
}
