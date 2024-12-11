using System.Runtime.Serialization;

namespace LantanaGroup.Link.QueryDispatch.Application.Models
{
    [DataContract]
    public class DataAcquisitionRequestedValue
    {
        [DataMember]
        public string PatientId { get; set; } = String.Empty;
        [DataMember]
        public List<ScheduledReport> ScheduledReports { get; set; } = new List<ScheduledReport>();
        [DataMember]
        public string QueryType { get; set; } = QueryTypes.Initial.ToString();
        public string ReportableEvent { get; set; } = String.Empty;
    }
}
