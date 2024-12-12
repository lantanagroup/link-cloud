using System.ComponentModel.DataAnnotations;

namespace LantanaGroup.Link.Census.Application.Models
{
    public class CensusConfigModel
    {
        [Required]
        public string FacilityId { get; set; }
        [Required]
        public string ScheduledTrigger { get; set; }
    }
}
