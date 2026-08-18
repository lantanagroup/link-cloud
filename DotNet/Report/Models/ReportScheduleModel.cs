using LantanaGroup.Link.Report.Data.Entities;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Integration.Report;

namespace LantanaGroup.Link.Report.Models;

public class ReportScheduleModel
{
    public Guid Id { get; set; }
    public DateTime CreateDate { get; set; }
    public DateTime? ModifyDate { get; set; }
    public string FacilityId { get; set; } = string.Empty;
    public DateTimeOffset ReportStartDate { get; set; }
    public DateTimeOffset ReportEndDate { get; set; }
    public DateTime? SubmitReportDateTime { get; set; }
    public bool EnableSubmission { get; set; } = true;
    public bool EndOfReportPeriodJobHasRun { get; set; } = false;
    public AdHocType? AdHocType { get; set; }
    public Frequency Frequency { get; set; }
    public string? PayloadRootUri { get; set; }
    public ScheduleStatus Status { get; set; } = ScheduleStatus.New;
    public bool? IsDeleted { get; set; } = false;

    public List<string> ReportTypes { get; set; } = new();

    internal static ReportScheduleModel FromEntity(ReportSchedule reportSchedule)
    {
        return new ReportScheduleModel
        {
            Id = reportSchedule.Id,
            FacilityId = reportSchedule.FacilityId,
            ReportStartDate = reportSchedule.ReportStartDate,
            ReportEndDate = reportSchedule.ReportEndDate,
            Frequency = reportSchedule.Frequency,
            AdHocType = reportSchedule.AdHocType,
            ReportTypes = reportSchedule.ReportTypes.Select(rt => rt.ReportType).ToList(),
            EndOfReportPeriodJobHasRun = reportSchedule.EndOfReportPeriodJobHasRun,
            EnableSubmission = reportSchedule.EnableSubmission,
            IsDeleted = reportSchedule.IsDeleted,
            PayloadRootUri = reportSchedule.PayloadRootUri,
            Status = reportSchedule.Status,
            SubmitReportDateTime = reportSchedule.SubmitReportDateTime,
            CreateDate = reportSchedule.CreateDate,
            ModifyDate = reportSchedule.ModifyDate,
        };
    }

    public ReportScheduleApiModel ToApiModel()
    {
        return new ReportScheduleApiModel
        {
            Id = Id,
            CreateDate = CreateDate,
            FacilityId = FacilityId,
            Frequency = Frequency,
            ReportStartDate = ReportStartDate.UtcDateTime,
            ReportEndDate = ReportEndDate.UtcDateTime,
            SubmitReportDateTime = SubmitReportDateTime,
            Status = Status,
            AdHocType = AdHocType,
            EndOfReportPeriodJobHasRun = EndOfReportPeriodJobHasRun,
            EnableSubmission = EnableSubmission,
            PayloadRootUri = PayloadRootUri,
            ReportTypes = ReportTypes,
            IsDeleted = IsDeleted,
        };
    }
}