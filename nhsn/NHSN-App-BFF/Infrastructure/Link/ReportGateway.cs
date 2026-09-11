using LantanaGroup.Link.Nhsn.App.Bff.Application.Interfaces.Infrastructure;
using LantanaGroup.Link.Nhsn.App.Bff.Application.Models;
using LantanaGroup.Link.Nhsn.App.Bff.Application.Models.Onboarding;
using LantanaGroup.Link.Nhsn.App.Bff.Application.Models.Reporting;
using LantanaGroup.Link.Sdk.Clients;
using LantanaGroup.Link.Shared.Application.Models.Integration.Report;

namespace LantanaGroup.Link.Nhsn.App.Bff.Infrastructure.Link;

// IReportGateway over LinkSdk's IReportServiceClient.
internal sealed class ReportGateway : IReportGateway
{
    private const string ServiceName = "Report";

    private readonly IReportServiceClient _reportClient;

    public ReportGateway(IReportServiceClient reportClient)
    {
        _reportClient = reportClient;
    }

    public async Task<ReportScheduleSummary?> GetLatestScheduleAsync(string facilityId, CancellationToken cancellationToken = default)
    {
        var response = await _reportClient.GetSchedulesByFacilityAsync(facilityId, cancellationToken: cancellationToken);
        var schedules = LinkResponseHandler.Optional(response, ServiceName, nameof(GetLatestScheduleAsync));
        if (schedules is null || schedules.Count == 0)
        {
            return null;
        }

        var latest = schedules.OrderByDescending(schedule => schedule.CreateDate ?? DateTime.MinValue).First();
        return new ReportScheduleSummary
        {
            ReportId = latest.Id.ToString(),
            Measures = latest.ReportTypes
        };
    }

    public async Task<Paged<ReportSummary>> ListReportsAsync(string facilityId, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var schedulesResponse = await _reportClient.GetSchedulesByFacilityAsync(facilityId, cancellationToken: cancellationToken);
        var schedules = LinkResponseHandler.Optional(schedulesResponse, ServiceName, nameof(ListReportsAsync)) ?? [];

        var ordered = schedules.OrderByDescending(schedule => schedule.CreateDate ?? DateTime.MinValue).ToList();
        var pageItems = ordered.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        var items = new List<ReportSummary>(pageItems.Count);
        foreach (var schedule in pageItems)
        {
            var summaryResponse = await _reportClient.GetReportSummaryAsync(schedule.Id.ToString(), cancellationToken);
            var summary = LinkResponseHandler.Optional(summaryResponse, ServiceName, nameof(ListReportsAsync));
            items.Add(ToDetail(schedule, summary));
        }

        return new Paged<ReportSummary>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = ordered.Count
        };
    }

    public async Task<ReportDetail?> GetReportAsync(string reportId, CancellationToken cancellationToken = default)
    {
        var scheduleResponse = await _reportClient.GetScheduleAsync(reportId, cancellationToken);
        var schedule = LinkResponseHandler.Optional(scheduleResponse, ServiceName, nameof(GetReportAsync));
        if (schedule is null)
        {
            return null;
        }

        var summaryResponse = await _reportClient.GetReportSummaryAsync(reportId, cancellationToken);
        var summary = LinkResponseHandler.Optional(summaryResponse, ServiceName, nameof(GetReportAsync));

        return ToDetail(schedule, summary);
    }

    public async Task<List<ReportPatientEntry>> GetReportPatientsAsync(string reportId, CancellationToken cancellationToken = default)
    {
        var response = await _reportClient.GetEntriesByScheduleAsync(reportId, cancellationToken);
        var entries = LinkResponseHandler.Optional(response, ServiceName, nameof(GetReportPatientsAsync)) ?? [];

        return entries.Select(ToPatientEntry).ToList();
    }

    public async Task<PatientMappingEvidence?> GetPatientMappingEvidenceAsync(string reportId, string patientId, CancellationToken cancellationToken = default)
    {
        var response = await _reportClient.GetEntryByScheduleAndPatientAsync(reportId, patientId, cancellationToken);
        var entry = LinkResponseHandler.Optional(response, ServiceName, nameof(GetPatientMappingEvidenceAsync));
        if (entry is null)
        {
            return null;
        }

        return new PatientMappingEvidence
        {
            LocationOrg = entry.Acquisition is null
                ? null
                : new LocationOrgEvidence
                {
                    EncounterCount = entry.Acquisition.LocationOrg.EncounterCount,
                    OrgEncounterCount = entry.Acquisition.LocationOrg.OrgEncounterCount,
                    AssumedOrgEncounterCount = entry.Acquisition.LocationOrg.AssumedOrgEncounterCount,
                    Matches = entry.Acquisition.LocationOrg.Matches.Select(match => new LocationOrgMatch
                    {
                        LocationId = match.LocationId,
                        LocationName = match.LocationName,
                        LocationAlias = match.LocationAlias,
                        PartOfValue = match.PartOfValue,
                        IsOrgLocation = match.IsOrgLocation
                    }).ToList()
                },
            CodeMaps = entry.Normalization?.CodeMaps.Select(codeMap => new CodeMapEvidence
            {
                SourceSystem = codeMap.SourceSystem,
                TargetSystem = codeMap.TargetSystem,
                MappedCount = codeMap.MappedCount,
                UnmappedCount = codeMap.UnmappedCount,
                FailureCount = codeMap.FailureCount,
                UnmappedCodes = codeMap.UnmappedCodes
            }).ToList() ?? []
        };
    }

    // Schedule carries CreateDate and the report window; summary (when Report has generated one
    // yet) carries the patient count and completion status. Falls back to the schedule's own
    // report types/status when Report has not produced a summary for it yet.
    private static ReportDetail ToDetail(ReportScheduleApiModel schedule, ReportSummaryApiModel? summary) => new()
    {
        ReportId = schedule.Id.ToString(),
        Measures = summary is {ReportTypes.Count: > 0} ? summary.ReportTypes : schedule.ReportTypes,
        PatientCount = summary?.PatientCount ?? 0,
        StartDate = schedule.ReportStartDate.ToString("O"),
        EndDate = schedule.ReportEndDate.ToString("O"),
        CreateDate = (schedule.CreateDate ?? schedule.ReportStartDate).ToString("O"),
        Status = ToUiStatus(summary?.Status),
        // rdMeasureMapping has no real endpoint yet -- DMRP's mapping proposal is still in
        // development, matching the onboarding POC's own note on this field.
        MeasureMapping = []
    };

    // Report's ReportStatus has no distinct "Failed" value -- Unknown (including "no summary yet")
    // is the closest fit and surfaces as the UI's Failed pill rather than silently reading Pending.
    private static string ToUiStatus(ReportStatus? status) => status switch
    {
        ReportStatus.Completed => "Complete",
        ReportStatus.Pending => "Pending",
        ReportStatus.Canceled => "Cancelled",
        _ => "Failed"
    };

    private static ReportPatientEntry ToPatientEntry(ReportEntryApiModel entry)
    {
        var resourceCountsByType = new Dictionary<string, int>();
        foreach (var measureReport in entry.MeasureReports)
        {
            foreach (var (resourceType, count) in measureReport.ResourceCount)
            {
                resourceCountsByType[resourceType] = resourceCountsByType.GetValueOrDefault(resourceType) + count;
            }
        }

        return new ReportPatientEntry
        {
            PatientId = entry.PatientId,
            ReportingStatus = entry.ReportingStatus.ToString(),
            ResourceCount = resourceCountsByType.Values.Sum(),
            ResourceCountsByType = resourceCountsByType,
            LocationOrgMapped = IsMapped(entry.LocationOrgStatus),
            EncounterMapped = IsMapped(entry.EncounterMappingStatus),
            HslocMapped = IsMapped(entry.HslocMappingStatus),
            // Validation has no per-patient result endpoint available to this BFF -- approximated
            // by whether validation has run for this patient at all.
            HasPreQualResults = entry.ReportingStatus is ReportingStatus.PassedValidation or ReportingStatus.FailedValidation,
            MeasureReports = entry.MeasureReports.Select(measureReport => new PatientMeasureReport
            {
                ReportType = measureReport.ReportType,
                ResourceCount = measureReport.ResourceCount.Values.Sum(),
                ResourceCountsByType = measureReport.ResourceCount
            }).ToList()
        };
    }

    public async Task<PatientMeasureReportExport?> GetPatientMeasureReportExportAsync(string reportId, string patientId, string reportType, CancellationToken cancellationToken = default)
    {
        var entryResponse = await _reportClient.GetEntryByScheduleAndPatientAsync(reportId, patientId, cancellationToken);
        var entry = LinkResponseHandler.Optional(entryResponse, ServiceName, nameof(GetPatientMeasureReportExportAsync));
        var measureReport = entry?.MeasureReports.FirstOrDefault(report => report.ReportType == reportType);
        if (entry is null || measureReport is null)
        {
            return null;
        }

        var resourcesResponse = await _reportClient.GetResourcesByScheduleAndPatientAsync(reportId, patientId, cancellationToken);
        var resources = LinkResponseHandler.Optional(resourcesResponse, ServiceName, nameof(GetPatientMeasureReportExportAsync)) ?? [];

        var scheduleResponse = await _reportClient.GetScheduleAsync(reportId, cancellationToken);
        var schedule = LinkResponseHandler.Optional(scheduleResponse, ServiceName, nameof(GetPatientMeasureReportExportAsync));

        return new PatientMeasureReportExport
        {
            PatientId = patientId,
            ReportType = reportType,
            MeasureReportId = measureReport.MeasureReportId,
            ReportingStatus = entry.ReportingStatus.ToString(),
            PeriodStart = schedule?.ReportStartDate,
            PeriodEnd = schedule?.ReportEndDate,
            ResourceCountsByType = measureReport.ResourceCount,
            EvaluatedResources = resources
                .Where(resource => resource.MeasureReportId == measureReport.MeasureReportId)
                .Select(resource => new EvaluatedResourceReference {ResourceType = resource.ResourceType, ResourceId = resource.ResourceId})
                .ToList()
        };
    }

    private static bool IsMapped(MappingIndicatorStatus status) =>
        status is MappingIndicatorStatus.Mapped or MappingIndicatorStatus.PartiallyMapped or MappingIndicatorStatus.Assumed;
}
