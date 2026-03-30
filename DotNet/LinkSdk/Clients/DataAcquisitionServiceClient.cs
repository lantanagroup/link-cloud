using Flurl.Http;
using LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition;
using LantanaGroup.Link.Shared.Application.Models.Responses;
using LantanaGroup.Link.Sdk.ApiClient;
using Newtonsoft.Json;
using System.Net.Http;

namespace LantanaGroup.Link.Sdk.Clients;

public sealed class DataAcquisitionServiceClient : LinkApiClientBase
{
    public DataAcquisitionServiceClient(ApiClientSettings settings)
        : base(settings)
    {
    }

    public Task GetFhirQueryConfigurationAsync(
        string facilityId,
        CancellationToken cancellationToken = default) =>
        Request($"data/{facilityId}/fhirQueryConfiguration")
            .GetAsync(cancellationToken: cancellationToken);

    public Task CreateFhirQueryConfigurationAsync(
        CreateFhirQueryConfigurationRequestApiModel request,
        CancellationToken cancellationToken = default) =>
        Request("data/fhirQueryConfiguration")
            .PostJsonAsync(request, cancellationToken: cancellationToken);

    public Task DeleteFhirQueryConfigurationAsync(
        string facilityId,
        CancellationToken cancellationToken = default) =>
        Request($"data/{facilityId}/fhirQueryConfiguration")
            .DeleteAsync(cancellationToken: cancellationToken);

    public Task GetQueryPlanAsync(
        string facilityId,
        string type,
        CancellationToken cancellationToken = default) =>
        Request($"data/{facilityId}/QueryPlan")
            .SetQueryParam("type", type)
            .GetAsync(cancellationToken: cancellationToken);

    public Task CreateQueryPlanAsync(
        string facilityId,
        CreateQueryPlanRequestApiModel request,
        CancellationToken cancellationToken = default) =>
        Request($"data/{facilityId}/QueryPlan")
            .WithHeader("Content-Type", "application/json")
            .SendStringAsync(HttpMethod.Post, JsonConvert.SerializeObject(request), cancellationToken: cancellationToken);

    public Task DeleteQueryPlanAsync(
        string facilityId,
        string type,
        CancellationToken cancellationToken = default) =>
        Request($"data/{facilityId}/QueryPlan")
            .SetQueryParam("type", type)
            .DeleteAsync(cancellationToken: cancellationToken);

    public Task<PagedConfigModel<DataAcquisitionLogApiModel>> SearchAcquisitionLogsAsync(
        string facilityId,
        string reportId,
        int pageSize = 100,
        int pageNumber = 1,
        CancellationToken cancellationToken = default) =>
        Request("data/acquisition-logs")
            .SetQueryParam("facilityId", facilityId)
            .SetQueryParam("reportId", reportId)
            .SetQueryParam("pageSize", pageSize)
            .SetQueryParam("pageNumber", pageNumber)
            .SetQueryParam("sortBy", "Id")
            .SetQueryParam("sortOrder", "Ascending")
            .GetJsonAsync<PagedConfigModel<DataAcquisitionLogApiModel>>(cancellationToken: cancellationToken);

    public Task<DataAcquisitionLogStatusStatisticsApiModel> GetReportStatusCountsAsync(
        string reportId,
        CancellationToken cancellationToken = default) =>
        Request($"data/acquisition-logs/report/{reportId}/status-counts")
            .GetJsonAsync<DataAcquisitionLogStatusStatisticsApiModel>(cancellationToken: cancellationToken);

    public Task<PagedConfigModel<DataAcquisitionLogApiModel>> SearchDetailedAcquisitionLogsAsync(
        string facilityId,
        string reportId,
        int pageSize = 100,
        int pageNumber = 1,
        CancellationToken cancellationToken = default) =>
        Request("data/acquisition-logs/detailed")
            .SetQueryParam("facilityId", facilityId)
            .SetQueryParam("reportId", reportId)
            .SetQueryParam("pageSize", pageSize)
            .SetQueryParam("pageNumber", pageNumber)
            .SetQueryParam("sortBy", "Id")
            .SetQueryParam("sortOrder", "Ascending")
            .GetJsonAsync<PagedConfigModel<DataAcquisitionLogApiModel>>(cancellationToken: cancellationToken);

    public Task<List<string>> GetAcquiredResourceIdsForReportAsync(
        string facilityId,
        string reportId,
        CancellationToken cancellationToken = default) =>
        Request($"data/acquisition-logs/report/{reportId}/acquired-resource-ids")
            .SetQueryParam("facilityId", facilityId)
            .GetJsonAsync<List<string>>(cancellationToken: cancellationToken);
}
