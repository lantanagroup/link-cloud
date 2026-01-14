using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Domain.Attributes;
using MongoDB.Bson.Serialization.Attributes;
using System.Text.Json.Serialization;

namespace LantanaGroup.Link.Report.Entities;

[BsonCollection("reportSchedule")]
[BsonIgnoreExtraElements]
public class ReportSchedule
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public DateTime CreateDate { get; set; }
    public DateTime? ModifyDate { get; set; }

    public string FacilityId { get; set; } = string.Empty;
    public DateTime ReportStartDate { get; set; }
    public DateTime ReportEndDate { get; set; }
    public DateTime? SubmitReportDateTime { get; set; } = null;
    public bool EnableSubmission { get; set; } = true;
    public bool EndOfReportPeriodJobHasRun { get; set; } = false;
    public List<string> ReportTypes { get; set; } = new List<string>();
    public Frequency Frequency { get; set; }
    public string? PayloadRootUri { get; set; }
    public ScheduleStatus Status { get; set; } = ScheduleStatus.New;
}