using LantanaGroup.Link.Report.Domain.Enums;
using LantanaGroup.Link.Shared.Domain.Attributes;
using LantanaGroup.Link.Shared.Domain.Entities;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace LantanaGroup.Link.Report.Entities
{
    [BsonCollection("reportEntry")]
    [BsonIgnoreExtraElements]
    public class ReportEntry : BaseEntityExtended
    {
        public string FacilityId { get; set; } = string.Empty;
        public string ReportScheduleId { get; set; } = string.Empty;
        public string PatientId { get; set; } = string.Empty;
        [BsonRepresentation(BsonType.String)]
        public ReportingStatus ReportingStatus { get; set; } = ReportingStatus.PatientIdentified;
        [BsonRepresentation(BsonType.String)]
        public SubmissionStatus? SubmissionStatus { get; set; } = null;
        public DateTime? SubmitReportDateTime { get; set; } = null;
        public string AggregateReportUri { get; set; } = string.Empty;
        public string AggregateReportBlobName { get; set; } = string.Empty;
        public List<EvaluatedMeasureReport> MeasureReportList { get; set; } = new List<EvaluatedMeasureReport>();
    }

    public class EvaluatedMeasureReport
    {
        public string MeasureReportId { get; set; } = string.Empty;
        [BsonRepresentation(BsonType.String)]
        public MeasureReportStatus Status { get; set; } = MeasureReportStatus.EntryCreated;
        public string ReportType { get; set; } = string.Empty;
        public string? MeasureReportUri { get; set; } = null;
        public string? MeasureReportFileName { get; set; } = null;
        //Key = ResourceType
        public Dictionary<string, int> ResourceCount { get; set; } = new Dictionary<string, int>();
    }
}