using LantanaGroup.Link.Shared.Application.Models.Kafka;
using System.ComponentModel.DataAnnotations;

namespace LantanaGroup.Link.LinkAdmin.BFF.Application.Interfaces.Models
{
    public interface IPatientListsAcquired
    {
        /// <summary>
        /// The unique identifier of the facility
       /// </summary>
       [Required]
        string FacilityId { get; set; }

        /// <summary>
       /// List of patient identifiers acquired from the facility
        /// </summary>
        [Required]
       List<PatientListItem> PatientLists { get; set; }
    }
}
