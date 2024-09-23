using LantanaGroup.Link.Shared.Domain.Attributes;
using LantanaGroup.Link.Shared.Domain.Entities;
using MongoDB.Bson.Serialization.Attributes;

namespace LantanaGroup.Link.Report.Entities
{

    [BsonCollection("reportSchedule")]
    [BsonIgnoreExtraElements]
    public class ReportScheduleModel : BaseEntityExtended
    {
        public string FacilityId { get; set; } = string.Empty;
        public DateTime ReportStartDate { get; set; }
        public DateTime ReportEndDate { get; set; }
        public DateTime? SubmitReportDateTime { get; set; }
        public DateTime? SubmittedDate { get; set; }
        public bool PatientsToQueryDataRequested { get; set; } = false;
        public string[] ReportTypes { get; set; } = Array.Empty<string>();
        public string Frequency { get; internal set; } = string.Empty;
    }
}
