using LantanaGroup.Link.Report.Domain.Enums;
using LantanaGroup.Link.Shared.Domain.Attributes;
using LantanaGroup.Link.Shared.Domain.Entities;
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
        [BsonRepresentation(MongoDB.Bson.BsonType.String)]
        public ReportingStatus ReportingStatus = ReportingStatus.PatientIdentified;
        [BsonRepresentation(MongoDB.Bson.BsonType.String)]
        public SubmissionStatus? SubmissionStatus = null;
        //TODO: Don't think we need uri anymore
        public string AggregateReportUri { get; set; } = string.Empty;
        public string AggregateReportBlobName { get; set; } = string.Empty;
        public List<EvaluatedMeasureReport> MeasureReportList { get; set; } = new List<EvaluatedMeasureReport>();
    }

    public class EvaluatedMeasureReport
    {
        public string MeasureReportId { get; set; } = string.Empty;
        [BsonRepresentation(MongoDB.Bson.BsonType.String)]
        public MeasureReportStatus Status { get; set; } = MeasureReportStatus.EntryCreated;
        public string ReportType { get; set; } = string.Empty;
        public string MeasureReportUri { get; set; } = string.Empty;
        public string MeasureReportFileName { get; set; } = string.Empty;
        //Key = ResourceType
        public Dictionary<string, int> ResourceCount = new Dictionary<string, int>();
    }
}