using System.ComponentModel.DataAnnotations;

namespace LantanaGroup.Link.Census.Application.Models
{
    public class CensusConfigModel
    {
        [Required]
        public string FacilityId { get; set; } = string.Empty;
        [Required]
        public string ScheduledTrigger { get; set; } = string.Empty;
    }
}
