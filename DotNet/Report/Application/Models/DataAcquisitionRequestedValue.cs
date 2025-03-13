using Hl7.Fhir.Model;
using LantanaGroup.Link.Report.Domain.Enums;

namespace LantanaGroup.Link.Report.Application.Models
{

    public class DataAcquisitionRequestedValue
    {
        public string PatientId { get; set; } = string.Empty;
        public string ReportableEvent { get; set; }
        public List<ScheduledReport> ScheduledReports { get; set; }
        public string QueryType { get; set; } = Domain.Enums.QueryType.Initial.ToString();
    }

    public class ScheduledReport
    {
        public List<string> ReportTypes { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public string Frequency { get; set; }
    }
}
