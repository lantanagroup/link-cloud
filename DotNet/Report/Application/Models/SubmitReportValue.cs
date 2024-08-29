using Hl7.Fhir.Model;

namespace LantanaGroup.Link.Report.Application.Models
{

    public class SubmitReportValue
    {
        public List<string>? PatientIds { get; internal set; }
        public Organization Organization { get; internal set; }
        public List<MeasureReport> Aggregates { get; internal set; }
        public List<string> MeasureIds { get; set; }
    }
}
