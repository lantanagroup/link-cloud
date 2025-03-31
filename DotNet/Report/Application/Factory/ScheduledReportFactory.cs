using Hl7.Fhir.Model;
using LantanaGroup.Link.Report.Domain.Enums;
using LantanaGroup.Link.Report.Entities;
using LantanaGroup.Link.Shared.Application.Models.Report;

namespace LantanaGroup.Link.Report.Application.Factory;

public class ScheduledReportFactory
{
    public ScheduledReportListSummary FromDomain(ReportScheduleModel reportScheduleModel)
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
}

public class MeasureReportSummaryFactory
{
    public MeasureReportSummary FromDomain(MeasureReportSubmissionEntryModel measureReport)
    {
        var summary = new MeasureReportSummary()
        {
            Id = measureReport.Id ?? string.Empty,
            PatientId = measureReport.PatientId,
            ReportType = measureReport.ReportType,
            Status = measureReport.Status.ToString(),
            ValidationStatus = measureReport.ValidationStatus.ToString(),
            ResourceCount = measureReport.ContainedResources.Count
        };
        
        summary.ResourceCountSummary = measureReport.ContainedResources
            .GroupBy(x => x.ResourceType)
            .ToDictionary(x => x.Key, x => x.Count());
        
        return summary;
    }
}

public class ResourceSummaryFactory
{
    public ResourceSummary FromDomain(string facilityId, string measureReportId, string patientId, MeasureReportSubmissionEntryModel.ContainedResource resource)
    {
        return new ResourceSummary()
        {
            FacilityId = facilityId,
            MeasureReportId = measureReportId,
            PatientId = patientId,
            ResourceId = resource.DocumentId,
            FhirId = resource.ResourceId,
            ResourceType = Enum.Parse<ResourceType>(resource.ResourceType),
            ResourceCategory = resource.CategoryType.ToString()
        };
    }
}