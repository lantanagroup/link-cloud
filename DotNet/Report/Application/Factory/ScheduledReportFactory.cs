using Hl7.Fhir.Model;
using LantanaGroup.Link.Report.Entities;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Report;

namespace LantanaGroup.Link.Report.Application.Factory;

public class ScheduledReportFactory
{
    /// <summary>
    /// Create a ScheduledReportListSummary populated from the provided ReportSchedule domain model.
    /// </summary>
    /// <param name="reportScheduleModel">The domain ReportSchedule to map into a summary object.</param>
    /// <returns>A ScheduledReportListSummary with fields copied from <paramref name="reportScheduleModel"/>; `Id` is the empty string if the source Id is null, and `Submitted` is `true` when `SubmitReportDateTime` has a value.</returns>
    public ScheduledReportListSummary FromDomain(ReportSchedule reportScheduleModel)
    {
        return new ScheduledReportListSummary()
        {
            Id = reportScheduleModel.Id ?? string.Empty,
            FacilityId = reportScheduleModel.FacilityId,
            ReportStartDate = reportScheduleModel.ReportStartDate,
            ReportEndDate = reportScheduleModel.ReportEndDate,
            Submitted = reportScheduleModel.SubmitReportDateTime.HasValue,
            SubmitDate = reportScheduleModel.SubmitReportDateTime,
            ReportTypes = reportScheduleModel.ReportTypes,
            Frequency = reportScheduleModel.Frequency,
            AdHocType = reportScheduleModel.AdHocType,
            PayloadRootUri = reportScheduleModel.PayloadRootUri,
            CreatedDate = reportScheduleModel.CreateDate
        };
    }
}

public class MeasureReportSummaryFactory
{
    public MeasureReportSummary FromDomain(PatientSubmissionEntry measureReport)
    {
        var summary = new MeasureReportSummary()
        {
            Id = measureReport.Id ?? string.Empty,
            ReportScheduleId = measureReport.ReportScheduleId,
            PatientId = measureReport.PatientId,
            ReportType = measureReport.ReportType,
            Status = measureReport.Status.ToString(),
            ValidationStatus = measureReport.ValidationStatus.ToString(),
        };
        
        
        return summary;
    }
}