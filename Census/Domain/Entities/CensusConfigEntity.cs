using LantanaGroup.Link.Shared.Domain.Entities;
using System.ComponentModel.DataAnnotations.Schema;

namespace Census.Domain.Entities
{
    [Table("CensusConfig")]
    public class CensusConfigEntity : BaseEntity
    {
        public string FacilityID { get; set; }
        public string ScheduledTrigger { get; set; }
        public DateTime CreateDate { get; set; }
        public DateTime ModifyDate { get; set; }
    }
}
