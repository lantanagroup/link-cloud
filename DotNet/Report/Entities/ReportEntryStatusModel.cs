using LantanaGroup.Link.Report.Domain.Enums;
using LantanaGroup.Link.Shared.Domain.Attributes;
using LantanaGroup.Link.Shared.Domain.Entities;
using MongoDB.Bson.Serialization.Attributes;

namespace LantanaGroup.Link.Report.Entities
{
    //TODO: Rename to patientReportingStatus?
    [BsonCollection("reportEntryStatus")]
    [BsonIgnoreExtraElements]
    public class ReportEntryStatusModel : BaseEntityExtended
    {
        public string FacilityId { get; set; } = string.Empty;
        public string ReportScheduleId { get; set; } = string.Empty;
        public string PatientId { get; set; } = string.Empty;
        //public string ReportType { get; set; } = string.Empty;
        //public PatientSubmissionStatus Status { get; set; } = PatientSubmissionStatus.PendingEvaluation;
        //public ValidationStatus ValidationStatus { get; set; } = ValidationStatus.Pending;
        //public string MeasureReportUri { get; set; } = string.Empty;
        //public string MeasureReportFileName { get; set; } = string.Empty;
        [BsonRepresentation(MongoDB.Bson.BsonType.String)]
        public ReportingStatus ReportingStatus = ReportingStatus.PatientIdentified;
        [BsonRepresentation(MongoDB.Bson.BsonType.String)]
        public SubmissionStatus? SubmissionStatus = null;
        public string AggregateReportUri { get; set; } = string.Empty;
        public string AggregateReportFileName { get; set; } = string.Empty;
        public List<MeasureReportEntry> MeasureReportEntryList { get; set; } = new List<MeasureReportEntry>();
    }

    public class MeasureReportEntry 
    {
        public string MeasureReportId { get; set; } = string.Empty;
        [BsonRepresentation(MongoDB.Bson.BsonType.String)]
        public MeasureReportStatus Status { get; set; } = MeasureReportStatus.PendingEvaluation;
        public string ReportType { get; set; } = string.Empty;
        public string MeasureReportUri { get; set; } = string.Empty;
        public string MeasureReportFileName { get; set; } = string.Empty;
    }
}
