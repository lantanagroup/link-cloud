using LantanaGroup.Link.Shared.Domain.Attributes;

namespace LantanaGroup.Link.Report.Entities
{
    [BsonCollection("patientSubmissionEntryResourceMap")]
    public class PatientSubmissionEntryResourceMap
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public required string ReportScheduleId { get; set; }   
        public required string SubmissionEntryId { get; set; }
        public required List<string> ReportTypes { get; set; }
        public required string ResourceType { get; set; }
        public required string ResourceId { get; set; }
        public string? FhirResourceId { get; set; }
        public DateTime CreateDate { get; set; }
        public DateTime? ModifyDate { get; set; }
    }
}
