using System.Runtime.Serialization;

namespace LantanaGroup.Link.Census.Application.Models
{
    [DataContract]
    public class CensusConfigModel
    {
        [DataMember]
        public string FacilityId { get; set; }
        [DataMember]
        public string ScheduledTrigger { get; set; }
    }
}