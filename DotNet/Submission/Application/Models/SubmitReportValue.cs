using Hl7.Fhir.Model;

namespace LantanaGroup.Link.Submission.Application.Models
{
    public class SubmitReportValue
    {
        public List<string>? PatientIds { get; internal set; }
        public Organization Organization { get; internal set; }
        public List<MeasureReport> Aggregates { get; internal set; }
        public Dictionary<string, List<string>> PatientReportIds { get; internal set; }
    }
}
