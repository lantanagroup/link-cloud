namespace LantanaGroup.Link.QueryDispatch.Application.Models
{
    public class DataAcquisitionRequestedValue
    {
        public string PatientId { get; set; } = String.Empty;
        public List<ScheduledReport> ScheduledReports { get; set; } = new List<ScheduledReport>();
        public string QueryType { get; set; } = QueryTypes.Initial.ToString();
        public string ReportableEvent { get; set; } = String.Empty;
    }
}
