using Hl7.Fhir.Model;
using LantanaGroup.Link.Report.Entities;

namespace LantanaGroup.Link.Report.Application.Models
{

    public class SubmissionReportValue
    {
        public List<string>? PatientIds { get; internal set; }
        public Organization Organization { get; internal set; }
        public List<MeasureReport> Aggregates { get; internal set; }
    }
}
