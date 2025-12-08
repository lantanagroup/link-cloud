using LantanaGroup.Link.Shared.Domain.Attributes;

namespace LantanaGroup.Link.Report.Entities
{
    [BsonCollection("reportScheduleResourceMap")]
    public class ReportScheduleResourceMap
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public required string ReportScheduleId { get; set; }
        public required List<string> ReportTypes { get; set; }
        public required string FhirResourceId { get; set; }
        public DateTime CreateDate { get; set; }
        public DateTime? ModifyDate { get; set; }
    }
}
