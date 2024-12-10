using System.Runtime.Serialization;

namespace LantanaGroup.Link.QueryDispatch.Application.Models
{
    [DataContract]
    public class ScheduledReport
    {
        [DataMember]
        public List<string> ReportTypes { get; set; }
        [DataMember]
        public string Frequency { get; set; }
        [DataMember]
        public DateTime StartDate { get; set; }
        [DataMember]
        public DateTime EndDate { get; set; }
    }
}
