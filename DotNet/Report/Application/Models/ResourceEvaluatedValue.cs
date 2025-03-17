using System.Text.Json;

namespace LantanaGroup.Link.Report.Application.Models
{

    public class ResourceEvaluatedValue
    {
        public string PatientId { get; set; } = string.Empty;
        public string ReportType { get; set; } = string.Empty;
        public JsonElement Resource { get; set; }
        public bool IsReportable { get; set; }
        public string ReportTrackingId { get; set; }
    }
}
