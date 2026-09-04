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

    public Task<LinkApiResponse<ReportScheduleApiModel>> GetScheduleAsync(string reportId, CancellationToken cancellationToken = default) =>
        SendAsync<ReportScheduleApiModel>(() => Request($"/schedules/{reportId}").GetAsync(cancellationToken: cancellationToken));

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

    public Task<LinkApiResponse> SoftDeleteScheduleAsync(string reportId, CancellationToken cancellationToken = default) =>
        SendAsync(() => Request($"/schedules/{reportId}").DeleteAsync(cancellationToken: cancellationToken));

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
