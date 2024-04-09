using System.ComponentModel.DataAnnotations.Schema;
using LantanaGroup.Link.Census.Domain.Entities;

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
