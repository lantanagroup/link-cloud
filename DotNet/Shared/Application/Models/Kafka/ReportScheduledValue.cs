using LantanaGroup.Link.DataAcquisition.Domain.Models;
using System.Runtime.Serialization;

namespace LantanaGroup.Link.Shared.Application.Models.Kafka
{
    public class ReportScheduledValue
    {
        [DataMember]
        public List<string> ReportTypes { get; set; }
        [DataMember]
        public Frequency Frequency { get; set; }
        [DataMember]
        public DateTimeOffset StartDate { get; set; }
        [DataMember]
        public DateTimeOffset EndDate { get; set; }
        [DataMember]
        public string ReportTrackingId { get; set; }

        public bool IsValid()
        {
            if (ReportTypes == null || ReportTypes.Count <= 0 || StartDate == default || EndDate == default || string.IsNullOrEmpty(ReportTrackingId))
            {
                return false;
            }

            return true;
        }
    }
}
