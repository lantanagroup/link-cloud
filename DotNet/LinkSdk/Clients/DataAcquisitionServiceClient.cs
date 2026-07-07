using Flurl.Http;
using LantanaGroup.Link.Sdk.ApiClient;
using LantanaGroup.Link.Shared.Application.Extensions.Security;
using LantanaGroup.Link.Shared.Application.Interfaces.Services.Security.Token;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition;
using LantanaGroup.Link.Shared.Application.Models.Responses;
using Microsoft.Extensions.Options;

namespace LantanaGroup.Link.Sdk.Clients;

public class DataAcquisitionServiceClient : LinkApiClientBase, IDataAcquisitionServiceClient
{
    public DataAcquisitionServiceClient(
        IOptions<ServiceRegistry> serviceRegistry,
        IOptions<BackendAuthenticationServiceExtension.LinkBearerServiceOptions> bearerOptions,
        IOptions<LinkTokenServiceSettings> tokenServiceSettings,
        ICreateSystemToken tokenService)
        : base(
            serviceRegistry.Value.DataAcquisitionServiceApiUrl
                ?? throw new InvalidOperationException("DataAcquisition service URL is not configured in ServiceRegistry."),
            bearerOptions, tokenServiceSettings, tokenService)
    { }

    public Task<LinkApiResponse> GetFhirQueryConfigurationAsync(
        string facilityId,
        CancellationToken cancellationToken = default) =>
        SendAsync(() => Request($"data/{facilityId}/fhirQueryConfiguration")
            .GetAsync(cancellationToken: cancellationToken));

    public Task<LinkApiResponse> CreateFhirQueryConfigurationAsync(
        CreateFhirQueryConfigurationRequestApiModel request,
        CancellationToken cancellationToken = default) =>
        SendAsync(() => Request("data/fhirQueryConfiguration")
            .PostJsonAsync(request, cancellationToken: cancellationToken));

    public Task<LinkApiResponse> DeleteFhirQueryConfigurationAsync(
        string facilityId,
        CancellationToken cancellationToken = default) =>
        SendAsync(() => Request($"data/{facilityId}/fhirQueryConfiguration")
            .DeleteAsync(cancellationToken: cancellationToken));

    public Task<LinkApiResponse> GetFhirListConfigurationAsync(
        string facilityId,
        CancellationToken cancellationToken = default) =>
        SendAsync(() => Request($"data/{facilityId}/fhirQueryList")
            .GetAsync(cancellationToken: cancellationToken));

    public Task<LinkApiResponse> CreateFhirListConfigurationAsync(
        object request,
        CancellationToken cancellationToken = default) =>
        SendAsync(() => Request("data/fhirQueryList")
            .PostJsonAsync(request, cancellationToken: cancellationToken));

    public Task<LinkApiResponse> DeleteFhirListConfigurationAsync(
        string facilityId,
        CancellationToken cancellationToken = default) =>
        SendAsync(() => Request($"data/{facilityId}/fhirQueryList")
            .DeleteAsync(cancellationToken: cancellationToken));

    public Task<LinkApiResponse> GetQueryPlanAsync(
        string facilityId,
        string type,
        CancellationToken cancellationToken = default) =>
        SendAsync(() => Request($"data/{facilityId}/QueryPlan")
            .SetQueryParam("type", type)
            .GetAsync(cancellationToken: cancellationToken));

    public Task<LinkApiResponse> CreateQueryPlanAsync(
        string facilityId,
        CreateQueryPlanRequestApiModel request,
        CancellationToken cancellationToken = default) =>
        SendAsync(() => Request($"data/{facilityId}/QueryPlan")
            .WithHeader("Content-Type", "application/json")
            .SendStringAsync(HttpMethod.Post,
                Newtonsoft.Json.JsonConvert.SerializeObject(request),
                cancellationToken: cancellationToken));

    public Task<LinkApiResponse> DeleteQueryPlanAsync(
        string facilityId,
        string type,
        CancellationToken cancellationToken = default) =>
        SendAsync(() => Request($"data/{facilityId}/QueryPlan")
            .SetQueryParam("type", type)
            .DeleteAsync(cancellationToken: cancellationToken));

    public Task<LinkApiResponse> SoftDeleteLogsByFacilityAsync(
        string facilityId,
        CancellationToken cancellationToken = default) =>
        SendAsync(() => Request($"data/acquisition-logs/facility/{facilityId}")
            .DeleteAsync(cancellationToken: cancellationToken));

    public Task<LinkApiResponse<PagedConfigModel<DataAcquisitionLogApiModel>>> SearchAcquisitionLogsAsync(
        string facilityId,
        string reportId,
        int pageSize = 100,
        int pageNumber = 1,
        string sortBy = "Id",
        string sortOrder = "Ascending",
        string? searchTerm = null,
        CancellationToken cancellationToken = default) =>
        SendAsync<PagedConfigModel<DataAcquisitionLogApiModel>>(() => Request("data/acquisition-logs")
            .SetQueryParam("facilityId", facilityId)
            .SetQueryParam("reportId", reportId)
            .SetQueryParam("pageSize", pageSize)
            .SetQueryParam("pageNumber", pageNumber)
            .SetQueryParam("sortBy", sortBy)
            .SetQueryParam("sortOrder", sortOrder)
            .SetQueryParam("searchTerm", string.IsNullOrWhiteSpace(searchTerm) ? null : searchTerm)
            .GetAsync(cancellationToken: cancellationToken));

    public Task<LinkApiResponse<DataAcquisitionLogApiModel>> GetAcquisitionLogByIdAsync(
        long id,
        CancellationToken cancellationToken = default) =>
        SendAsync<DataAcquisitionLogApiModel>(() => Request($"data/acquisition-logs/{id}")
            .GetAsync(cancellationToken: cancellationToken));

    public Task<LinkApiResponse<List<string>>> GetAcquisitionLogNotesAsync(
        long id,
        CancellationToken cancellationToken = default) =>
        SendAsync<List<string>>(() => Request($"data/acquisition-logs/{id}/notes")
            .GetAsync(cancellationToken: cancellationToken));

    public Task<LinkApiResponse<DataAcquisitionLogStatusStatisticsApiModel>> GetReportStatusCountsAsync(
        string reportId,
        CancellationToken cancellationToken = default) =>
        SendAsync<DataAcquisitionLogStatusStatisticsApiModel>(() => Request($"data/acquisition-logs/report/{reportId}/status-counts")
            .GetAsync(cancellationToken: cancellationToken));

    public Task<LinkApiResponse> GetReportStatisticsAsync(
        string reportId,
        CancellationToken cancellationToken = default) =>
        SendAsync(() => Request($"data/acquisition-logs/report/{reportId}/statistics")
            .GetAsync(cancellationToken: cancellationToken));

    public Task<LinkApiResponse<DataAcquisitionReportSummaryApiModel>> GetReportSummaryAsync(
        string reportId,
        CancellationToken cancellationToken = default) =>
        SendAsync<DataAcquisitionReportSummaryApiModel>(() => Request($"data/acquisition-logs/report/{reportId}/summary")
            .GetAsync(cancellationToken: cancellationToken));

    public Task<LinkApiResponse<List<string>>> GetAcquiredResourceIdsForReportAsync(
        string facilityId,
        string reportId,
        CancellationToken cancellationToken = default) =>
        SendAsync<List<string>>(() => Request($"data/acquisition-logs/report/{reportId}/acquired-resource-ids")
            .SetQueryParam("facilityId", facilityId)
            .GetAsync(cancellationToken: cancellationToken));

    public Task<LinkApiResponse<PagedConfigModel<ReferenceResourceApiModel>>> GetReferenceResourcesForLogAsync(
        long logId,
        int pageSize = 100,
        int pageNumber = 1,
        CancellationToken cancellationToken = default) =>
        SendAsync<PagedConfigModel<ReferenceResourceApiModel>>(() => Request($"data/acquisition-logs/{logId}/reference-resources")
            .SetQueryParam("pageSize", pageSize)
            .SetQueryParam("pageNumber", pageNumber)
            .GetAsync(cancellationToken: cancellationToken));

    public Task<LinkApiResponse> ProcessAcquisitionLogAsync(
        long id,
        CancellationToken cancellationToken = default) =>
        SendAsync(() => Request($"data/acquisition-logs/{id}/process")
            .PostJsonAsync(id, cancellationToken: cancellationToken));

    public Task<LinkApiResponse> ProcessAcquisitionLogsBulkAsync(
        List<long> ids,
        CancellationToken cancellationToken = default) =>
        SendAsync(() => Request("data/acquisition-logs/process-bulk")
            .PostJsonAsync(ids, cancellationToken: cancellationToken));

    public Task<LinkApiResponse<DataAcquisitionBulkActionResultApiModel>> CancelAcquisitionLogsBulkAsync(
        List<long> ids,
        int minAgeHours = 24,
        CancellationToken cancellationToken = default) =>
        SendAsync<DataAcquisitionBulkActionResultApiModel>(() => Request("data/acquisition-logs/cancel-bulk")
            .SetQueryParam("minAgeHours", minAgeHours)
            .PostJsonAsync(ids, cancellationToken: cancellationToken));

    public Task<LinkApiResponse> ProcessAcquisitionLogsByFilterAsync(
        object filter,
        CancellationToken cancellationToken = default) =>
        SendAsync(() => Request("data/acquisition-logs/process-by-filter")
            .PostJsonAsync(filter, cancellationToken: cancellationToken));

    public Task<LinkApiResponse<DataAcquisitionBulkActionResultApiModel>> CancelAcquisitionLogsByFilterAsync(
        object filter,
        int minAgeHours = 24,
        CancellationToken cancellationToken = default) =>
        SendAsync<DataAcquisitionBulkActionResultApiModel>(() => Request("data/acquisition-logs/cancel-by-filter")
            .SetQueryParam("minAgeHours", minAgeHours)
            .PostJsonAsync(filter, cancellationToken: cancellationToken));

    public Task<LinkApiResponse> DeleteAcquisitionLogAsync(
        long id,
        CancellationToken cancellationToken = default) =>
        SendAsync(() => Request($"data/acquisition-logs/{id}")
            .DeleteAsync(cancellationToken: cancellationToken));

    public Task<LinkApiResponse> SoftDeleteLogsByReportTrackingIdAsync(
        string reportTrackingId,
        CancellationToken cancellationToken = default) =>
        SendAsync(() => Request($"data/acquisition-logs/report/{reportTrackingId}")
            .DeleteAsync(cancellationToken: cancellationToken));

    public Task<LinkApiResponse> RestoreLogsByReportTrackingIdAsync(
        string reportTrackingId,
        CancellationToken cancellationToken = default) =>
        SendAsync(() => Request($"data/acquisition-logs/report/{reportTrackingId}/restore")
            .PatchJsonAsync(new { }, cancellationToken: cancellationToken));

    public Task<LinkApiResponse> RestoreLogsByFacilityAsync(
        string facilityId,
        CancellationToken cancellationToken = default) =>
        SendAsync(() => Request($"data/acquisition-logs/facility/{facilityId}/restore")
            .PatchJsonAsync(new { }, cancellationToken: cancellationToken));

    public Task<LinkApiResponse<List<OrganizationLocationConfigurationApiModel>>> GetOrganizationLocationConfigurationsAsync(
        string facilityId,
        CancellationToken cancellationToken = default) =>
        SendAsync<List<OrganizationLocationConfigurationApiModel>>(() => Request($"data/location-config/facility/{facilityId}")
            .GetAsync(cancellationToken: cancellationToken));

    public Task<LinkApiResponse<OrganizationLocationConfigurationApiModel>> CreateOrganizationLocationConfigurationAsync(
        string facilityId,
        CreateOrganizationLocationConfigurationApiModel request,
        CancellationToken cancellationToken = default) =>
        SendAsync<OrganizationLocationConfigurationApiModel>(() => Request($"data/location-config/facility/{facilityId}")
            .PostJsonAsync(request, cancellationToken: cancellationToken));

    public Task<LinkApiResponse<List<OrganizationLocationMappingApiModel>>> GetOrganizationLocationMappingsAsync(
        string facilityId,
        CancellationToken cancellationToken = default) =>
        SendAsync<List<OrganizationLocationMappingApiModel>>(() => Request($"data/location-mappings/facility/{facilityId}")
            .GetAsync(cancellationToken: cancellationToken));
}
