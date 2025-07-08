using Hl7.Fhir.Model;

namespace LantanaGroup.Link.Shared.Application.Models
{
    public class SubmitReportValue
    {
        public List<string>? PatientIds { get; set; }
        public Organization Organization { get; set; } = new Organization();
        public List<MeasureReport> Aggregates { get; set; } = new();
        public List<string> MeasureIds { get; set; } = new();
        public string ReportTrackingId { get; set; } = string.Empty;
    }
}
