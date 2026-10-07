using Flurl.Http;
using LantanaGroup.Link.Sdk.ApiClient;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Extensions.Security;
using LantanaGroup.Link.Shared.Application.Interfaces.Services.Security.Token;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.Models.Integration.Report;
using LantanaGroup.Link.Shared.Application.Models.Responses;
using LantanaGroup.Link.Shared.Application.Models.Tenant;
using Microsoft.Extensions.Options;

namespace LantanaGroup.Link.Sdk.Clients;

public class ReportServiceClient : LinkApiClientBase, IReportServiceClient
{
    public ReportServiceClient(
        IOptions<ServiceRegistry> serviceRegistry,
        IOptions<BackendAuthenticationServiceExtension.LinkBearerServiceOptions> bearerOptions,
        IOptions<LinkTokenServiceSettings> tokenServiceSettings,
        ICreateSystemToken tokenService)
        : base(
            serviceRegistry.Value.ReportServiceApiUrl
                ?? throw new InvalidOperationException("Report service URL is not configured in ServiceRegistry."),
            bearerOptions, tokenServiceSettings, tokenService)
    { }

    public Task<LinkApiResponse<ReportScheduleApiModel>> GetScheduleAsync(string reportId, CancellationToken cancellationToken = default, bool includeDeleted = false) =>
        SendAsync<ReportScheduleApiModel>(() => Request($"/schedules/{reportId}")
            .SetQueryParam("includeDeleted", includeDeleted ? "true" : null)
            .GetAsync(cancellationToken: cancellationToken));

    public Task<LinkApiResponse<List<ReportScheduleApiModel>>> GetSchedulesByFacilityAsync(string facilityId, bool? active = null, bool blocking = false, bool includeDeleted = false, CancellationToken cancellationToken = default)
    {
        var r = Request($"/schedules/facilities/{facilityId}")
            .SetQueryParam("blocking", blocking)
            .SetQueryParam("includeDeleted", includeDeleted);
        if (active.HasValue) r = r.SetQueryParam("active", active.Value);
        return SendAsync<List<ReportScheduleApiModel>>(() => r.GetAsync(cancellationToken: cancellationToken));
    }

    public Task<LinkApiResponse<PagedConfigModel<ReportScheduleApiModel>>> SearchSchedulesAsync(string reportId, CancellationToken cancellationToken = default) =>
        SendAsync<PagedConfigModel<ReportScheduleApiModel>>(() => Request("/schedules/search").SetQueryParam("id", reportId).SetQueryParam("pageSize", 10).SetQueryParam("pageNumber", 1).GetAsync(cancellationToken: cancellationToken));

    public Task<LinkApiResponse<PagedConfigModel<ReportScheduleApiModel>>> SearchFacilitySchedulesAsync(
        ReportScheduleSearch query,
        CancellationToken cancellationToken = default)
    {
        var pageSize = query.PageSize is < 1 or > 100 ? 10 : query.PageSize;
        var pageNumber = query.PageNumber < 1 ? 1 : query.PageNumber;
        var request = Request("/schedules/search")
            .SetQueryParam("pageSize", pageSize)
            .SetQueryParam("pageNumber", pageNumber)
            .SetQueryParam("facilityId", query.FacilityId)
            .SetQueryParam("frequency", query.Frequency?.ToString())
            .SetQueryParam("reportType", query.ReportType)
            .SetQueryParam("reportStartDate", query.ReportStartDate?.ToString("yyyy-MM-dd"))
            .SetQueryParam("reportEndDate", query.ReportEndDate?.ToString("yyyy-MM-ddTHH:mm:ss"))
            .SetQueryParam("includeDeleted", query.IncludeDeleted ? "true" : null)
            .SetQueryParam("sortBy", query.SortBy)
            .SetQueryParam("sortOrder", query.SortOrder?.ToString())
            .SetQueryParam("createDate", query.CreateDate?.ToString("yyyy-MM-dd"))
            .SetQueryParam("id", query.Id?.ToString());

        if (query.Statuses is { Count: > 0 })
        {
            foreach (var status in query.Statuses)
                request.Url.QueryParams.Add("status", status.ToString());
        }

        return SendAsync<PagedConfigModel<ReportScheduleApiModel>>(() => request.GetAsync(cancellationToken: cancellationToken));
    }

    public Task<LinkApiResponse<ReportActivityCounts>> GetActivityCountsAsync(
        ReportActivityCountRequest request,
        CancellationToken cancellationToken = default) =>
        SendAsync<ReportActivityCounts>(() => Request("/schedules/counts")
            .PostJsonAsync(request, cancellationToken: cancellationToken));

    public Task<LinkApiResponse<PagedConfigModel<ReportSummaryApiModel>>> GetReportSummariesAsync(
        string? facilityId = null,
        ReportStatus? status = null,
        string? sortBy = null,
        SortOrder? sortOrder = null,
        int pageSize = 10,
        int pageNumber = 1,
        CancellationToken cancellationToken = default)
    {
        var request = Request("/schedules/summaries")
            .SetQueryParam("pageSize", pageSize)
            .SetQueryParam("pageNumber", pageNumber);

        if (!string.IsNullOrWhiteSpace(facilityId)) request = request.SetQueryParam("facilityId", facilityId);
        if (status.HasValue) request = request.SetQueryParam("status", status.Value);
        if (!string.IsNullOrWhiteSpace(sortBy)) request = request.SetQueryParam("sortBy", sortBy);
        if (sortOrder.HasValue) request = request.SetQueryParam("sortOrder", sortOrder.Value);

        return SendAsync<PagedConfigModel<ReportSummaryApiModel>>(() => request.GetAsync(cancellationToken: cancellationToken));
    }

    public Task<LinkApiResponse<ReportSummaryApiModel>> GetReportSummaryAsync(string reportScheduleId, CancellationToken cancellationToken = default) =>
        SendAsync<ReportSummaryApiModel>(() => Request($"/schedules/{reportScheduleId}/summary").GetAsync(cancellationToken: cancellationToken));

    public Task<LinkApiResponse> SoftDeleteScheduleAsync(string reportId, CancellationToken cancellationToken = default, bool allowInProgress = false)
    {
        var request = Request($"/schedules/{reportId}");
        if (allowInProgress)
            request = request.SetQueryParam("allowInProgress", "true");
        return SendAsync(() => request.DeleteAsync(cancellationToken: cancellationToken));
    }

    public Task<LinkApiResponse> RestoreScheduleAsync(string reportId, CancellationToken cancellationToken = default) =>
        SendAsync(() => Request($"/schedules/{reportId}/restore").PatchAsync(cancellationToken: cancellationToken));

    public Task<LinkApiResponse> SetReportsDeletedStatusForFacilityAsync(string facilityId, bool deleted, CancellationToken cancellationToken = default) =>
        SendAsync(() => Request($"/schedules/facility/{facilityId}/status").SetQueryParam("deleted", deleted).PatchAsync(cancellationToken: cancellationToken));

    public Task<LinkApiResponse<ReportEntryApiModel>> GetEntryByIdAsync(string id, CancellationToken cancellationToken = default) =>
        SendAsync<ReportEntryApiModel>(() => Request($"/entries/{id}").GetAsync(cancellationToken: cancellationToken));

    public Task<LinkApiResponse<List<ReportEntryApiModel>>> GetEntriesByScheduleAsync(string reportId, CancellationToken cancellationToken = default) =>
        SendAsync<List<ReportEntryApiModel>>(() => Request($"/entries/schedules/{reportId}").GetAsync(cancellationToken: cancellationToken));

    public Task<LinkApiResponse<List<ReportEntryApiModel>>> GetEntriesByPatientAsync(string patientId, CancellationToken cancellationToken = default) =>
        SendAsync<List<ReportEntryApiModel>>(() => Request($"/entries/patients/{patientId}").GetAsync(cancellationToken: cancellationToken));

    public Task<LinkApiResponse<int>> GetEntryCountByScheduleAsync(string reportScheduleId, CancellationToken cancellationToken = default) =>
        SendAsync<int>(() => Request($"/entries/schedules/{reportScheduleId}/count").GetAsync(cancellationToken: cancellationToken));

    public Task<LinkApiResponse<ReportEntrySummaryApiModel>> GetEntrySummaryByScheduleAsync(string reportScheduleId, CancellationToken cancellationToken = default) =>
        SendAsync<ReportEntrySummaryApiModel>(() => Request($"/entries/schedules/{reportScheduleId}/summary").GetAsync(cancellationToken: cancellationToken));

    public Task<LinkApiResponse<ReportEntryDetailApiModel>> GetEntryByScheduleAndPatientAsync(string reportScheduleId, string patientId, CancellationToken cancellationToken = default) =>
        SendAsync<ReportEntryDetailApiModel>(() => Request($"/entries/schedules/{reportScheduleId}/patients/{patientId}").GetAsync(cancellationToken: cancellationToken));

    public Task<LinkApiResponse<PagedConfigModel<ReportEntryApiModel>>> SearchEntriesAsync(
        string? facilityId = null,
        string? patientId = null,
        string? reportScheduleId = null,
        string? reportType = null,
        string? sortBy = null,
        int pageSize = 10,
        int pageNumber = 1,
        CancellationToken cancellationToken = default) =>
        SendAsync<PagedConfigModel<ReportEntryApiModel>>(() => Request("/entries/search")
            .SetQueryParam("facilityId", facilityId)
            .SetQueryParam("patientId", patientId)
            .SetQueryParam("reportScheduleId", reportScheduleId)
            .SetQueryParam("reportType", reportType)
            .SetQueryParam("sortBy", sortBy)
            .SetQueryParam("pageSize", pageSize)
            .SetQueryParam("pageNumber", pageNumber)
            .GetAsync(cancellationToken: cancellationToken));

    public Task<LinkApiResponse<ReportResourceApiModel>> GetResourceByIdAsync(string id, CancellationToken cancellationToken = default) =>
        SendAsync<ReportResourceApiModel>(() => Request($"/resources/{id}").GetAsync(cancellationToken: cancellationToken));

    public Task<LinkApiResponse<List<ReportResourceApiModel>>> GetResourcesByScheduleAsync(string reportScheduleId, CancellationToken cancellationToken = default) =>
        SendAsync<List<ReportResourceApiModel>>(() => Request($"/resources/schedules/{reportScheduleId}").GetAsync(cancellationToken: cancellationToken));

    public Task<LinkApiResponse<List<ReportResourceApiModel>>> GetResourcesByScheduleAndPatientAsync(string reportScheduleId, string patientId, CancellationToken cancellationToken = default) =>
        SendAsync<List<ReportResourceApiModel>>(() => Request($"/resources/schedules/{reportScheduleId}/patients/{patientId}").GetAsync(cancellationToken: cancellationToken));

    public Task<LinkApiResponse<List<ReportResourceApiModel>>> GetResourcesByPatientAsync(string patientId, CancellationToken cancellationToken = default) =>
        SendAsync<List<ReportResourceApiModel>>(() => Request($"/resources/patients/{patientId}").GetAsync(cancellationToken: cancellationToken));

    public Task<LinkApiResponse<PagedConfigModel<ReportResourceApiModel>>> SearchResourcesAsync(string facilityId, string reportId, int pageSize = 5000, int pageNumber = 1, CancellationToken cancellationToken = default) =>
        SendAsync<PagedConfigModel<ReportResourceApiModel>>(() => Request("/resources/search").SetQueryParam("facilityId", facilityId).SetQueryParam("reportScheduleId", reportId).SetQueryParam("pageSize", pageSize).SetQueryParam("pageNumber", pageNumber).GetAsync(cancellationToken: cancellationToken));

    public Task<LinkApiResponse<ReportPopulationApiModel>> GetPopulationByIdAsync(string id, CancellationToken cancellationToken = default) =>
        SendAsync<ReportPopulationApiModel>(() => Request($"/populations/{id}").GetAsync(cancellationToken: cancellationToken));

    public Task<LinkApiResponse<List<ReportPopulationApiModel>>> GetPopulationsByScheduleAsync(string reportId, string? reportType = null, CancellationToken cancellationToken = default)
    {
        var r = Request($"/populations/schedules/{reportId}");
        if (!string.IsNullOrWhiteSpace(reportType)) r = r.SetQueryParam("reportType", reportType);
        return SendAsync<List<ReportPopulationApiModel>>(() => r.GetAsync(cancellationToken: cancellationToken));
    }

    public Task<LinkApiResponse<int>> GetInitialPopulationCountAsync(string reportScheduleId, CancellationToken cancellationToken = default) =>
        SendAsync<int>(() => Request($"/populations/schedules/{reportScheduleId}/initial-population-count").GetAsync(cancellationToken: cancellationToken));
}
