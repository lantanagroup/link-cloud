using Flurl.Http;
using LantanaGroup.Link.Sdk.ApiClient;
using LantanaGroup.Link.Shared.Application.Extensions.Security;
using LantanaGroup.Link.Shared.Application.Interfaces.Services.Security.Token;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition;
using RequestStatus = LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition.RequestStatus;
using QueryPhase = LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition.QueryPhase;
using FhirQueryType = LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition.FhirQueryType;
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

    public Task GetFhirQueryConfigurationAsync(
        string facilityId,
        CancellationToken cancellationToken = default) =>
        Request($"data/{facilityId}/fhirQueryConfiguration")
            .GetAsync(cancellationToken: cancellationToken);

    public Task<bool> HasFhirQueryConfigurationAsync(
        string facilityId,
        CancellationToken cancellationToken = default) =>
        ExistsAsync(() => Request($"data/{facilityId}/fhirQueryConfiguration")
            .GetAsync(cancellationToken: cancellationToken));

    public Task<bool> CreateFhirQueryConfigurationAsync(
        CreateFhirQueryConfigurationRequestApiModel request,
        CancellationToken cancellationToken = default) =>
        CreateOrExistsAsync(() => Request("data/fhirQueryConfiguration")
            .PostJsonAsync(request, cancellationToken: cancellationToken));

    public Task DeleteFhirQueryConfigurationAsync(
        string facilityId,
        CancellationToken cancellationToken = default) =>
        DeleteOrIgnoreAsync(() => Request($"data/{facilityId}/fhirQueryConfiguration")
            .DeleteAsync(cancellationToken: cancellationToken));

    public Task GetQueryPlanAsync(
        string facilityId,
        string type,
        CancellationToken cancellationToken = default) =>
        Request($"data/{facilityId}/QueryPlan")
            .SetQueryParam("type", type)
            .GetAsync(cancellationToken: cancellationToken);

    public Task<bool> HasQueryPlanAsync(
        string facilityId,
        string type,
        CancellationToken cancellationToken = default) =>
        ExistsAsync(() => Request($"data/{facilityId}/QueryPlan")
            .SetQueryParam("type", type)
            .GetAsync(cancellationToken: cancellationToken));

    public Task<bool> CreateQueryPlanAsync(
        string facilityId,
        CreateQueryPlanRequestApiModel request,
        CancellationToken cancellationToken = default) =>
        CreateOrExistsAsync(() => Request($"data/{facilityId}/QueryPlan")
            .WithHeader("Content-Type", "application/json")
            .SendStringAsync(HttpMethod.Post,
                Newtonsoft.Json.JsonConvert.SerializeObject(request),
                cancellationToken: cancellationToken));

    public Task DeleteQueryPlanAsync(
        string facilityId,
        string type,
        CancellationToken cancellationToken = default) =>
        DeleteOrIgnoreAsync(() => Request($"data/{facilityId}/QueryPlan")
            .SetQueryParam("type", type)
            .DeleteAsync(cancellationToken: cancellationToken));

    public Task SoftDeleteLogsByFacilityAsync(
        string facilityId,
        CancellationToken cancellationToken = default) =>
        DeleteOrIgnoreAsync(() => Request($"data/acquisition-logs/facility/{facilityId}")
            .DeleteAsync(cancellationToken: cancellationToken));

    public Task<PagedConfigModel<DataAcquisitionLogApiModel>> SearchAcquisitionLogsAsync(
        string facilityId,
        string reportId,
        int pageSize = 100,
        int pageNumber = 1,
        string sortBy = "Id",
        string sortOrder = "Ascending",
        CancellationToken cancellationToken = default) =>
        Request("data/acquisition-logs")
            .SetQueryParam("facilityId", facilityId)
            .SetQueryParam("reportId", reportId)
            .SetQueryParam("pageSize", pageSize)
            .SetQueryParam("pageNumber", pageNumber)
            .SetQueryParam("sortBy", sortBy)
            .SetQueryParam("sortOrder", sortOrder)
            .GetJsonAsync<PagedConfigModel<DataAcquisitionLogApiModel>>(cancellationToken: cancellationToken);

    public Task<DataAcquisitionLogApiModel?> GetAcquisitionLogByIdAsync(
        long id,
        CancellationToken cancellationToken = default) =>
        GetOrDefaultAsync(() => Request($"data/acquisition-logs/{id}")
            .GetJsonAsync<DataAcquisitionLogApiModel>(cancellationToken: cancellationToken));

    public Task<DataAcquisitionLogStatusStatisticsApiModel?> GetReportStatusCountsAsync(
        string reportId,
        CancellationToken cancellationToken = default) =>
        GetOrDefaultAsync(() => Request($"data/acquisition-logs/report/{reportId}/status-counts")
            .GetJsonAsync<DataAcquisitionLogStatusStatisticsApiModel>(cancellationToken: cancellationToken));

    public Task<DataAcquisitionReportSummaryApiModel?> GetReportSummaryAsync(
        string reportId,
        CancellationToken cancellationToken = default) =>
        GetOrDefaultAsync(() => Request($"data/acquisition-logs/report/{reportId}/summary")
            .GetJsonAsync<DataAcquisitionReportSummaryApiModel>(cancellationToken: cancellationToken));

    public Task<List<string>> GetAcquiredResourceIdsForReportAsync(
        string facilityId,
        string reportId,
        CancellationToken cancellationToken = default) =>
        Request($"data/acquisition-logs/report/{reportId}/acquired-resource-ids")
            .SetQueryParam("facilityId", facilityId)
            .GetJsonAsync<List<string>>(cancellationToken: cancellationToken);

    public Task<PagedConfigModel<ReferenceResourceApiModel>> GetReferenceResourcesForLogAsync(
        long logId,
        int pageSize = 100,
        int pageNumber = 1,
        CancellationToken cancellationToken = default) =>
        Request($"data/acquisition-logs/{logId}/reference-resources")
            .SetQueryParam("pageSize", pageSize)
            .SetQueryParam("pageNumber", pageNumber)
            .GetJsonAsync<PagedConfigModel<ReferenceResourceApiModel>>(cancellationToken: cancellationToken);
}
