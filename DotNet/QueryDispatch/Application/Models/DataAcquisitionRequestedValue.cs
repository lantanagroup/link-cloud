namespace LantanaGroup.Link.QueryDispatch.Application.Models
{
    public class DataAcquisitionRequestedValue
    {
        public string PatientId { get; set; }
        public List<ScheduledReport> ScheduledReports { get; set; }
    }
}
