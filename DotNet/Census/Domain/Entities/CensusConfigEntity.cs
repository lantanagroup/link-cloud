using LantanaGroup.Link.Shared.Domain.Entities;
using System.ComponentModel.DataAnnotations.Schema;

namespace Census.Domain.Entities
{
    [Table("CensusConfig")]
    public class CensusConfigEntity
    {
        public Guid Id { get; set; }
        public string FacilityID { get; set; }
        public string ScheduledTrigger { get; set; }
        public bool? Enabled { get; set; } = true;
        public DateTime CreateDate { get; set; } = DateTime.UtcNow;
        public DateTime? ModifyDate { get; set; }
    }
}
