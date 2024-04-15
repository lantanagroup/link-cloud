using System.Text.Json;
using Hl7.Fhir.Model;

namespace LantanaGroup.Link.Report.Application.Models
{

    public class ResourceEvaluatedValue
    {
        public string PatientId { get; set; } = string.Empty;
        public string MeasureReportId { get; set; } = string.Empty;
        public string Resource { get; set; }
    }
}
