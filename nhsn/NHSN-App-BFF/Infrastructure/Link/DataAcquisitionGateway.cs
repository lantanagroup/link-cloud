using LantanaGroup.Link.Nhsn.App.Bff.Application.Interfaces.Infrastructure;
using LantanaGroup.Link.Nhsn.App.Bff.Application.Models.Reporting;
using LantanaGroup.Link.Sdk.Clients;
using LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition;

namespace LantanaGroup.Link.Nhsn.App.Bff.Infrastructure.Link;

// IDataAcquisitionGateway over LinkSdk's IDataAcquisitionServiceClient.
internal sealed class DataAcquisitionGateway : IDataAcquisitionGateway
{
    private const string ServiceName = "DataAcquisition";

    private readonly IDataAcquisitionServiceClient _dataAcquisitionClient;

    public DataAcquisitionGateway(IDataAcquisitionServiceClient dataAcquisitionClient)
    {
        _dataAcquisitionClient = dataAcquisitionClient;
    }

    public async Task<QueryPlan?> GetQueryPlanAsync(string facilityId, string type, CancellationToken cancellationToken = default)
    {
        var response = await _dataAcquisitionClient.GetQueryPlanAsync(facilityId, type, cancellationToken);
        if (response.StatusCode == StatusCodes.Status404NotFound)
        {
            return null;
        }
        LinkResponseHandler.EnsureSuccess(response, ServiceName, nameof(GetQueryPlanAsync));

        return new QueryPlan
        {
            ReportId = facilityId,
            PlanJson = response.RawBody ?? "{}"
        };
    }

    // A report can generate more logs than fit on one page -- loop rather than fetch page 1 only,
    // capped so a runaway report can't turn this into an unbounded fan-out of upstream calls.
    // DataAcquisition rejects any PageSize above 100 (400: "PageSize must be between 1 and 100"),
    // so 20 pages covers up to 2000 records -- well beyond what the report step's 10-patient cap
    // (see patientIds.ts) can realistically generate.
    private const int LogPageSize = 100;
    private const int MaxLogPages = 20;

    public async Task<List<AcquisitionLogEntry>> GetAcquisitionLogsAsync(string facilityId, string reportId, CancellationToken cancellationToken = default)
    {
        var logs = new List<DataAcquisitionLogApiModel>();
        for (var pageNumber = 1; pageNumber <= MaxLogPages; pageNumber++)
        {
            var response = await _dataAcquisitionClient.SearchAcquisitionLogsAsync(
                facilityId, reportId, pageSize: LogPageSize, pageNumber: pageNumber, cancellationToken: cancellationToken);
            var page = LinkResponseHandler.Optional(response, ServiceName, nameof(GetAcquisitionLogsAsync));
            if (page is null || page.Records.Count == 0)
            {
                break;
            }

            logs.AddRange(page.Records);
            if (pageNumber >= page.Metadata.TotalPages)
            {
                break;
            }
        }

        return logs.SelectMany(ToAcquisitionLogEntries).ToList();
    }

    // One DataAcquisition log can cover several FHIR queries (e.g. one per referenced Location) --
    // one row per query, matching the granularity of the onboarding POC's acquisition log table.
    // A log with no FhirQuery entries yet (queued, not yet dispatched) still gets one row so its
    // status is visible.
    private static IEnumerable<AcquisitionLogEntry> ToAcquisitionLogEntries(DataAcquisitionLogApiModel log)
    {
        var patientId = log.PatientId ?? string.Empty;
        var queryPhase = log.QueryPhase?.ToString() ?? string.Empty;
        var status = log.Status?.ToString() ?? "Unknown";

        if (log.FhirQuery.Count == 0)
        {
            yield return new AcquisitionLogEntry
            {
                PatientId = patientId,
                Resource = string.Join(", ", log.ResourceTypes),
                QueryPhase = queryPhase,
                QueryType = null,
                Parameters = [],
                Status = status
            };
            yield break;
        }

        foreach (var query in log.FhirQuery)
        {
            yield return new AcquisitionLogEntry
            {
                PatientId = patientId,
                Resource = string.Join(", ", query.ResourceTypes),
                QueryPhase = queryPhase,
                QueryType = query.QueryType.ToString(),
                Parameters = query.QueryParameters,
                Status = status
            };
        }
    }

    public async Task<AcquisitionReportSummary?> GetReportSummaryAsync(string reportId, CancellationToken cancellationToken = default)
    {
        var response = await _dataAcquisitionClient.GetReportSummaryAsync(reportId, cancellationToken);
        var summary = LinkResponseHandler.Optional(response, ServiceName, nameof(GetReportSummaryAsync));
        if (summary is null)
        {
            return null;
        }

        return new AcquisitionReportSummary
        {
            ReportId = summary.ReportId,
            TotalLogs = summary.TotalLogs,
            TotalPatients = summary.TotalPatients,
            TotalCompletedPatients = summary.TotalCompletedPatients,
            TotalResourcesAcquired = summary.TotalResourcesAcquired,
            StatusCounts = summary.StatusCounts.Select(s => new AcquisitionStatusCount {Status = s.Status, Count = s.Count}).ToList(),
            ResourceTypeCounts = summary.ResourceTypeCounts.Select(r => new AcquisitionResourceTypeCount {ResourceType = r.ResourceType, Count = r.Count}).ToList()
        };
    }
}
