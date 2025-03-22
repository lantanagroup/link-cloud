using Hl7.Fhir.Model;
using LantanaGroup.Link.Report.Domain.Enums;
using LantanaGroup.Link.Report.Entities;
using LantanaGroup.Link.Shared.Application.Models.Report;

namespace LantanaGroup.Link.Report.Application.Factory;

public class ScheduledReportFactory
{
    public ScheduledReportListSummary FromDomainToListSummary(ReportScheduleModel reportScheduleModel)
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
            Frequency = reportScheduleModel.Frequency
        };
    }
    
    public ScheduledReportSummary FromDomainToSummary(ReportScheduleModel reportScheduleModel)
    {
        return new ScheduledReportSummary()
        {
            Id = reportScheduleModel.Id ?? string.Empty,
            FacilityId = reportScheduleModel.FacilityId,
            ReportStartDate = reportScheduleModel.ReportStartDate,
            ReportEndDate = reportScheduleModel.ReportEndDate,
            Submitted = reportScheduleModel.SubmitReportDateTime.HasValue,
            SubmitDate = reportScheduleModel.SubmitReportDateTime,
            ReportTypes = reportScheduleModel.ReportTypes,
            Frequency = reportScheduleModel.Frequency
        };
    }
}

public class PatientReportSummaryFactory
{
    public PatientReportSummary FromDomain(MeasureReportSubmissionEntryModel measureReport)
    {
        return new PatientReportSummary()
        {
            Id = measureReport.Id ?? string.Empty,
            PatientId = measureReport.PatientId,
            ReportType = measureReport.ReportType,
            Status = measureReport.Status.ToString(),
            ValidationStatus = measureReport.ValidationStatus.ToString()
        };
    }
}