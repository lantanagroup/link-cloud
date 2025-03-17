
using LantanaGroup.Link.DataAcquisition.Domain.Models;
using System.Runtime.Serialization;

namespace LantanaGroup.Link.Shared.Application.Models
{
    [DataContract]
    public class ScheduledReport
    {
        [DataMember]
        public List<string> ReportTypes { get; set; }
        [DataMember]
        public Frequency Frequency { get; set; }
        [DataMember]
        public DateTime StartDate { get; set; }
        [DataMember]
        public DateTime EndDate { get; set; }
        [DataMember]
        public string ReportTrackingId { get; set; }
    }
}
