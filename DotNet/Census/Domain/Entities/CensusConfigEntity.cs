using LantanaGroup.Link.Shared.Domain.Entities;
using System.ComponentModel.DataAnnotations.Schema;

namespace Census.Domain.Entities
{
    [Table("CensusConfig")]
    public class CensusConfigEntity : BaseEntityExtended
    {
        public string FacilityID { get; set; }
        public string ScheduledTrigger { get; set; }
        public bool? Enabled { get; set; } = true;
    }
}
