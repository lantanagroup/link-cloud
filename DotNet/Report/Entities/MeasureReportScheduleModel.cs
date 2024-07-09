using LantanaGroup.Link.Shared.Domain.Attributes;
using LantanaGroup.Link.Shared.Domain.Entities;
using MongoDB.Bson.Serialization.Attributes;

namespace LantanaGroup.Link.Report.Entities
{

    [BsonCollection("measureReportSchedule")]
    [BsonIgnoreExtraElements]
    public class MeasureReportScheduleModel : BaseEntity
    {
        public string FacilityId { get; set; } = string.Empty;
        public DateTime ReportStartDate { get; set; }
        public DateTime ReportEndDate { get; set; }
        public List<string>? PatientsToQuery { get; set; }
        public DateTime? SubmittedDate { get; set; }
        public bool? PatientsToQueryDataRequested { get; set; } = false;
        public string ReportType { get; internal set; } = string.Empty;
    }
}
