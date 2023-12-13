using LantanaGroup.Link.Shared.Domain.Attributes;
using LantanaGroup.Link.Shared.Domain.Entities;

namespace Census.Domain.Entities
{
    [BsonCollection("censusConfigEntity")]
    public class CensusConfigEntity : BaseEntity
    {
        public string FacilityID { get; set; }
        public string ScheduledTrigger { get; set; }
        public DateTime CreateDate { get; set; }
        public DateTime ModifyDate { get; set; }
    }
}
