namespace LantanaGroup.Link.PatientsToQuery.Application.Models
{
    public class DataAcquisitionRequestedValue
    {
        public string PatientId { get; set; }
        public List<ScheduledReport> ScheduledReports { get; set; }
        public string QueryType { get; set; }
    }
}
