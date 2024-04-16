using System.Text.Json;

namespace LantanaGroup.Link.Report.Application.Models
{

    public class ResourceEvaluatedValue
    {
        public string PatientId { get; set; } = string.Empty;
        public string MeasureReportId { get; set; } = string.Empty;
        public JsonElement Resource { get; set; }
    }
}
