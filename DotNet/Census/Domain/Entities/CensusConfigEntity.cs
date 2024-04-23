using LantanaGroup.Link.Census.Domain.Entities;
using System.ComponentModel.DataAnnotations.Schema;

namespace Census.Domain.Entities
{
    [Table("CensusConfig")]
    public class CensusConfigEntity : BaseEntity
    {
        public string FacilityID { get; set; }
        public string ScheduledTrigger { get; set; }
    }
}
