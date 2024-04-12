using LantanaGroup.Link.Report.Domain.Enums;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson;

namespace LantanaGroup.Link.Report.Application.Models
{
    public class MeasureReportConfig
    {
        public string Id { get; set; } = string.Empty;
        public string FacilityId { get; set; } = string.Empty;
        public string ReportType { get; set; } = string.Empty;
        public string BundlingType { get; set; }
    }
}
