using LantanaGroup.Link.Shared.Domain.Entities;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Census.Domain.Entities
{
    [Table("CensusConfig")]
    public class CensusConfig
    {
        [Key]
        public Guid Id { get; set; }

        [Required]
        [Column("FacilityID")]
        public string FacilityId { get; set; }

        [Required]
        public string ScheduledTrigger { get; set; }

        public DateTime CreateDate { get; set; } = DateTime.UtcNow;

        public DateTime? ModifyDate { get; set; }

        public bool? Enabled { get; set; }
    }
}
