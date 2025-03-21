

namespace LantanaGroup.Link.Shared.Application.Models.Kafka
{
    public class GenerateReportValue
    {
        public string? ReportId { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public List<string>? ReportTypes { get; set; }
        public List<string>? PatientIds { get; set; }
        public bool BypassSubmission { get; set; }
    }
}
