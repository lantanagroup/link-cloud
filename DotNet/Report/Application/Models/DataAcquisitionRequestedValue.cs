using Hl7.Fhir.Model;
using LantanaGroup.Link.Report.Entities;

namespace LantanaGroup.Link.Report.Application.Models
{

    public class DataAcquisitionRequestedValue
    {
        public string PatientId { get; set; } = string.Empty;
        public List<ScheduledReport> ScheduledReports { get; set; }
    }

    public class ScheduledReport
    {
        public string ReportType { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
    }
}
