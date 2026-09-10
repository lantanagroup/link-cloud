using LantanaGroup.Link.Nhsn.App.Bff.Application.Interfaces.Infrastructure;
using LantanaGroup.Link.Nhsn.App.Bff.Application.Models.Reporting;
using LantanaGroup.Link.Sdk.Clients;

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

    public async Task<QueryPlan?> GetQueryPlanAsync(string facilityId, string vendorType, CancellationToken cancellationToken = default)
    {
        var response = await _dataAcquisitionClient.GetQueryPlanAsync(facilityId, vendorType, cancellationToken);
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

    public async Task<List<AcquisitionLogEntry>> GetAcquisitionLogsAsync(string facilityId, string reportId, CancellationToken cancellationToken = default)
    {
        var response = await _dataAcquisitionClient.SearchAcquisitionLogsAsync(facilityId, reportId, pageSize: 500, cancellationToken: cancellationToken);
        var page = LinkResponseHandler.Optional(response, ServiceName, nameof(GetAcquisitionLogsAsync));
        var records = page?.Records ?? [];

        return records.Select(log => new AcquisitionLogEntry
        {
            Timestamp = (log.CompletionDate ?? DateTime.UtcNow).ToString("O"),
            Level = log.Status?.ToString() ?? "Unknown",
            Message = $"{string.Join(", ", log.ResourceTypes)} -- {log.ReferenceResourceCount} resource(s){(log.Notes is {Count: > 0} notes ? $" ({string.Join("; ", notes)})" : string.Empty)}"
        }).ToList();
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
