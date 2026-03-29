using System.Net;
using LantanaGroup.Link.Sdk.Clients;

namespace LantanaGroup.Link.Automation.Helpers;

public class PipelineDataReader
{
    private readonly ReportServiceClient _reportClient;
    private readonly DataAcquisitionServiceClient _dataAcqClient;
    private readonly NormalizationServiceClient _normalizationClient;
    private readonly FacilityServiceClient _facilityClient;

    public PipelineDataReader(
        ReportServiceClient reportClient,
        DataAcquisitionServiceClient dataAcqClient,
        NormalizationServiceClient normalizationClient,
        FacilityServiceClient facilityClient)
    {
        _reportClient = reportClient;
        _dataAcqClient = dataAcqClient;
        _normalizationClient = normalizationClient;
        _facilityClient = facilityClient;
    }

    public record ResourceGroupSummary(string PatientId, string ResourceType, int Count);
    public record ReportResourceIdentity(string PatientId, string ResourceType, string ResourceId);
    public record PatientResourceTypeCount(string PatientId, string ResourceType, int Count);

    public record ReportScheduleInfo(
        string? FacilityId,
        string? Status,
        string? Frequency,
        string? AdHocType,
        bool EnableSubmission,
        bool EndOfReportPeriodJobHasRun,
        string? PayloadRootUri,
        DateTime? ReportStartDate,
        DateTime? ReportEndDate);

    public record MeasureReportInfo(string? MeasureReportId, string? Status, string? ReportType, List<ResourceCountInfo> ResourceCounts);
    public record ResourceCountInfo(string ResourceType, int ResourceCount);
    public record ReportEntryInfo(Guid Id, string? FacilityId, string PatientId, string? ReportingStatus, string? SubmissionStatus, List<MeasureReportInfo> MeasureReports);
    public record EntryMeasureReportInfo(Guid Id, string? ReportType, string? MeasureReportId, string? Status, string PatientId, List<ResourceCountInfo> ResourceCounts);
    public record ScheduleReportTypeInfo(string ReportType);
    public record ReportPopulationInfo(string? ReportType, List<GroupPopulationInfo> GroupPopulations);
    public record GroupPopulationInfo(string? PopulationCodeJson, List<MeasureReportPopulationInfo> MeasureReportPopulations);
    public record MeasureReportPopulationInfo(string? MeasureReportId);

    public record AcquisitionLogInfo(long Id, string? PatientId, string? Status, string? QueryPhase, List<string> Notes, List<string> ResourceAcquiredIds, List<FhirQueryInfo> FhirQueries);
    public record QueryPlanInfo(string Type, string? PlanName, int InitialQueriesCount, int SupplementalQueriesCount);
    public record FhirQueryInfo(List<string> ResourceTypes);

    public record OperationInfo(string? Id, string? OperationType, string? Name, string? OperationJson, bool IsDisabled, List<string> ResourceTypes);
    public record OperationSequenceInfo(string? Id, int? Sequence, string? OperationType, string? ResourceType);

    public record FacilityScheduledReports(string[] Monthly, string[] Daily, string[] Weekly);
    public record FacilityInfo(string FacilityId, string? FacilityName, string? TimeZone, bool IsDeleted, DateTime? CreateDate, FacilityScheduledReports? ScheduledReports);

    public async Task<ReportScheduleInfo?> GetReportScheduleAsync(Guid scheduleId)
    {
        var (status, page) = await _reportClient.SearchSchedulesAsync(scheduleId.ToString());
        if (status != HttpStatusCode.OK)
            return null;

        var record = page?.Records?.FirstOrDefault();
        if (record == null)
            return null;

        return new ReportScheduleInfo(
            record.FacilityId,
            record.Status.ToString(),
            record.Frequency.ToString(),
            record.AdHocType.ToString(),
            record.EnableSubmission,
            record.EndOfReportPeriodJobHasRun,
            record.PayloadRootUri,
            record.ReportStartDate,
            record.ReportEndDate);
    }

    public Task<List<ReportEntryInfo>> GetReportEntriesAsync(Guid scheduleId)
        => GetReportEntriesWithMeasureReportsAsync(scheduleId);

    public async Task<List<ReportEntryInfo>> GetReportEntriesWithMeasureReportsAsync(Guid scheduleId)
    {
        var (status, entries) = await _reportClient.GetEntriesByScheduleAsync(scheduleId.ToString());
        if (status != HttpStatusCode.OK || entries == null)
            return [];

        return entries.Select(e =>
        {
            var mrs = e.MeasureReports.Select(mr => new MeasureReportInfo(
                mr.MeasureReportId,
                null,
                mr.ReportType,
                mr.ResourceCount.Select(rc => new ResourceCountInfo(rc.Key, rc.Value)).ToList())).ToList();

            return new ReportEntryInfo(
                e.Id,
                e.FacilityId,
                e.PatientId,
                e.ReportingStatus.ToString(),
                e.SubmissionStatus?.ToString(),
                mrs);
        }).ToList();
    }

    public async Task<List<EntryMeasureReportInfo>> GetEntryMeasureReportsAsync(Guid scheduleId)
    {
        var entries = await GetReportEntriesWithMeasureReportsAsync(scheduleId);
        return entries
            .SelectMany(e => e.MeasureReports.Select(mr => new EntryMeasureReportInfo(
                Guid.NewGuid(),
                mr.ReportType,
                mr.MeasureReportId,
                mr.Status,
                e.PatientId,
                mr.ResourceCounts)))
            .ToList();
    }

    public async Task<List<ScheduleReportTypeInfo>> GetScheduleReportTypesAsync(Guid scheduleId)
    {
        var measureReports = await GetEntryMeasureReportsAsync(scheduleId);
        return measureReports
            .Where(x => !string.IsNullOrWhiteSpace(x.ReportType))
            .Select(x => x.ReportType!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(x => new ScheduleReportTypeInfo(x))
            .ToList();
    }

    public async Task<List<ResourceGroupSummary>> GetReportResourceSummaryAsync(Guid scheduleId, string facilityId)
    {
        var identities = await GetReportResourceIdentitiesAsync(scheduleId, facilityId);
        return identities
            .GroupBy(r => new { r.PatientId, r.ResourceType })
            .Select(g => new ResourceGroupSummary(g.Key.PatientId, g.Key.ResourceType, g.Count()))
            .OrderBy(x => x.PatientId).ThenBy(x => x.ResourceType)
            .ToList();
    }

    public async Task<List<ReportPopulationInfo>> GetReportPopulationsAsync(Guid scheduleId, string facilityId)
    {
        var (status, pops) = await _reportClient.GetPopulationsByScheduleAsync(scheduleId.ToString());
        if (status != HttpStatusCode.OK || pops == null)
            return [];

        return pops.Select(p => new ReportPopulationInfo(
            p.ReportType,
            p.GroupPopulations.Select(gp => new GroupPopulationInfo(
                gp.PopulationCodeJson,
                gp.MeasureReportPopulations.Select(mrp => new MeasureReportPopulationInfo(mrp.MeasureReportId)).ToList())).ToList())).ToList();
    }

    public async Task<List<ReportResourceIdentity>> GetReportResourceIdentitiesAsync(Guid scheduleId, string facilityId)
    {
        var pageNumber = 1;
        const int pageSize = 100;
        var results = new List<ReportResourceIdentity>();

        while (true)
        {
            var (status, page) = await _reportClient.SearchResourcesAsync(facilityId, scheduleId.ToString(), pageSize: pageSize, pageNumber: pageNumber);
            if (status != HttpStatusCode.OK || page?.Records == null || page.Records.Count == 0)
                break;

            results.AddRange(page.Records.Select(r => new ReportResourceIdentity(r.PatientId, r.ResourceType, r.ResourceId)));

            if (page.Records.Count < pageSize)
                break;

            pageNumber++;
        }

        return results;
    }

    public async Task<List<ReportEntryInfo>> GetSubmittedReportEntriesAsync(Guid scheduleId)
    {
        var entries = await GetReportEntriesWithMeasureReportsAsync(scheduleId);
        return entries.Where(e => string.Equals(e.SubmissionStatus, "Submitted", StringComparison.OrdinalIgnoreCase)).ToList();
    }

    public async Task<List<AcquisitionLogInfo>> GetAcquisitionLogsAsync(string facilityId, string reportId)
    {
        var pageNumber = 1;
        const int pageSize = 100;
        var results = new List<AcquisitionLogInfo>();

        while (true)
        {
            var (status, page) = await _dataAcqClient.SearchDetailedAcquisitionLogsAsync(facilityId, reportId, pageSize: pageSize, pageNumber: pageNumber);
            if (status != HttpStatusCode.OK || page?.Records == null || page.Records.Count == 0)
                break;

            results.AddRange(page.Records.Select(log => new AcquisitionLogInfo(
                log.Id,
                log.PatientId,
                log.Status?.ToString(),
                log.QueryPhase?.ToString(),
                log.Notes?.ToList() ?? [],
                log.ResourceAcquiredIds?.ToList() ?? [],
                log.FhirQuery.Select(fq => new FhirQueryInfo(fq.ResourceTypes.Where(r => !string.IsNullOrWhiteSpace(r)).ToList())).ToList())));

            if (page.Records.Count < pageSize)
                break;

            pageNumber++;
        }

        return results;
    }

    public async Task<bool> HasFhirQueryConfigurationAsync(string facilityId)
    {
        var status = await _dataAcqClient.GetFhirQueryConfigurationAsync(facilityId);
        return status == HttpStatusCode.OK;
    }

    public async Task<List<QueryPlanInfo>> GetQueryPlansAsync(string facilityId)
    {
        var list = new List<QueryPlanInfo>();
        foreach (var type in new[] { "Discharge", "Monthly" })
        {
            var status = await _dataAcqClient.GetQueryPlanAsync(facilityId, type);
            if (status == HttpStatusCode.OK)
                list.Add(new QueryPlanInfo(type, null, 1, 1));
        }

        return list;
    }

    public async Task<List<FhirQueryInfo>> GetFhirQueriesForReportAsync(string facilityId, string reportId)
    {
        var logs = await GetAcquisitionLogsAsync(facilityId, reportId);
        return logs.SelectMany(l => l.FhirQueries).ToList();
    }

    public async Task<int> GetReferenceResourceGroupCountAsync(string facilityId, string reportId)
    {
        var logs = await GetAcquisitionLogsAsync(facilityId, reportId);

        return logs
            .Where(l => string.Equals(l.Status, "Completed", StringComparison.OrdinalIgnoreCase))
            .SelectMany(l => (l.ResourceAcquiredIds ?? [])
                .Where(id => !string.IsNullOrWhiteSpace(id) && id.Contains('/'))
                .Select(id => new
                {
                    ResourceType = id.Split('/')[0],
                    QueryPhase = l.QueryPhase ?? string.Empty
                }))
            .Where(x => !string.IsNullOrWhiteSpace(x.ResourceType))
            .Distinct()
            .Count();
    }

    public async Task<List<OperationInfo>> GetOperationsAsync(string facilityId)
    {
        var pageNumber = 1;
        const int pageSize = 100;
        var results = new List<OperationInfo>();

        while (true)
        {
            var (status, page) = await _normalizationClient.SearchFacilityOperationsAsync(facilityId, includeDisabled: true, pageSize: pageSize, pageNumber: pageNumber);
            if (status != HttpStatusCode.OK || page?.Records == null || page.Records.Count == 0)
                break;

            results.AddRange(page.Records.Select(op => new OperationInfo(
                op.Id.ToString(),
                op.OperationType,
                op.Name,
                op.OperationJson,
                op.IsDisabled,
                op.OperationResourceTypes
                    .Select(ort => ort.Resource?.ResourceName ?? string.Empty)
                    .Where(r => !string.IsNullOrWhiteSpace(r))
                    .ToList())));

            if (page.Records.Count < pageSize)
                break;

            pageNumber++;
        }

        return results;
    }

    public async Task<List<OperationSequenceInfo>> GetOperationSequencesAsync(string facilityId)
    {
        var (status, sequences) = await _normalizationClient.GetOperationSequencesAsync(facilityId);
        if (status != HttpStatusCode.OK || sequences == null)
            return [];

        return sequences.Select(s => new OperationSequenceInfo(
            s.Id.ToString(),
            s.Sequence,
            s.OperationResourceType?.Operation?.OperationType,
            s.OperationResourceType?.ResourceType?.ResourceName)).ToList();
    }

    public async Task<FacilityInfo?> GetFacilityAsync(string facilityId)
    {
        var (status, facility) = await _facilityClient.GetDetailsAsync(facilityId);
        if (status != HttpStatusCode.OK || facility == null)
            return null;

        return new FacilityInfo(
            facility.FacilityId ?? facilityId,
            facility.FacilityName,
            facility.TimeZone,
            facility.IsDeleted ?? false,
            null,
            new FacilityScheduledReports(
                facility.ScheduledReports?.Monthly ?? [],
                facility.ScheduledReports?.Daily ?? [],
                facility.ScheduledReports?.Weekly ?? []));
    }

    public async Task<List<PatientResourceTypeCount>> GetReportResourceCountsByPatientTypeAsync(Guid scheduleId, string facilityId)
    {
        var identities = await GetReportResourceIdentitiesAsync(scheduleId, facilityId);
        return identities
            .Where(r => !string.IsNullOrWhiteSpace(r.PatientId) && !string.IsNullOrWhiteSpace(r.ResourceType))
            .GroupBy(r => new { r.PatientId, r.ResourceType })
            .Select(g => new PatientResourceTypeCount(g.Key.PatientId, g.Key.ResourceType, g.Count()))
            .ToList();
    }

    public async Task<List<PatientResourceTypeCount>> GetMeasureEvalResourceCountsByPatientTypeAsync(Guid scheduleId)
    {
        var entries = await GetReportEntriesWithMeasureReportsAsync(scheduleId);
        var rows = entries
            .SelectMany(e => e.MeasureReports.SelectMany(mr => mr.ResourceCounts.Select(rc =>
                new PatientResourceTypeCount(e.PatientId, rc.ResourceType, rc.ResourceCount))))
            .ToList();

        return rows
            .GroupBy(r => new { r.PatientId, r.ResourceType })
            .Select(g => new PatientResourceTypeCount(g.Key.PatientId, g.Key.ResourceType, g.Sum(x => x.Count)))
            .ToList();
    }

    public async Task<List<PatientResourceTypeCount>> GetDataAcquisitionResourceCountsByPatientTypeAsync(string facilityId, string reportId)
    {
        var logs = await GetAcquisitionLogsAsync(facilityId, reportId);
        return logs
            .Where(l => !string.IsNullOrWhiteSpace(l.PatientId) && string.Equals(l.Status, "Completed", StringComparison.OrdinalIgnoreCase))
            .SelectMany(l => l.ResourceAcquiredIds
                .Where(id => !string.IsNullOrWhiteSpace(id) && id.Contains('/'))
                .Select(id => new { PatientId = l.PatientId!, ResourceType = id.Split('/')[0] }))
            .GroupBy(x => new { x.PatientId, x.ResourceType })
            .Select(g => new PatientResourceTypeCount(g.Key.PatientId, g.Key.ResourceType, g.Count()))
            .ToList();
    }

    public async Task<HashSet<string>> GetAcquiredResourceIdsForReportAsync(string facilityId, string reportId)
    {
        var (status, ids) = await _dataAcqClient.GetAcquiredResourceIdsForReportAsync(facilityId, reportId);
        if (status != HttpStatusCode.OK || ids == null)
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        return ids
            .Where(x => !string.IsNullOrWhiteSpace(x) && x.Contains('/'))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }
}
