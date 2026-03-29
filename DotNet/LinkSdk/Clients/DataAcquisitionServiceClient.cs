using Flurl.Http;
using LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition;
using LantanaGroup.Link.Shared.Application.Models.Responses;
using LantanaGroup.Link.Sdk.ApiClient;
using Newtonsoft.Json;
using System.Net;
using System.Net.Http;

namespace LantanaGroup.Link.Sdk.Clients;

public sealed class DataAcquisitionServiceClient : LinkApiClientBase
{
    public DataAcquisitionServiceClient(ApiClientSettings settings)
        : base(settings)
    {
    }

    public async Task<HttpStatusCode> GetFhirQueryConfigurationAsync(string facilityId, CancellationToken cancellationToken = default)
    {
        var response = await Request($"data/{facilityId}/fhirQueryConfiguration")
            .AllowAnyHttpStatus()
            .GetAsync(cancellationToken: cancellationToken);

        return response.ResponseMessage.StatusCode;
    }

    public async Task<HttpStatusCode> CreateFhirQueryConfigurationAsync(CreateFhirQueryConfigurationRequestApiModel request, CancellationToken cancellationToken = default)
    {
        var response = await Request("data/fhirQueryConfiguration")
            .AllowAnyHttpStatus()
            .PostJsonAsync(request, cancellationToken: cancellationToken);

        return response.ResponseMessage.StatusCode;
    }

    public async Task<HttpStatusCode> DeleteFhirQueryConfigurationAsync(string facilityId, CancellationToken cancellationToken = default)
    {
        var response = await Request($"data/{facilityId}/fhirQueryConfiguration")
            .AllowAnyHttpStatus()
            .DeleteAsync(cancellationToken: cancellationToken);

        return response.ResponseMessage.StatusCode;
    }

    public async Task<HttpStatusCode> GetQueryPlanAsync(string facilityId, string type, CancellationToken cancellationToken = default)
    {
        var response = await Request($"data/{facilityId}/QueryPlan")
            .SetQueryParam("type", type)
            .AllowAnyHttpStatus()
            .GetAsync(cancellationToken: cancellationToken);

        return response.ResponseMessage.StatusCode;
    }

    public async Task<HttpStatusCode> CreateQueryPlanAsync(string facilityId, CreateQueryPlanRequestApiModel request, CancellationToken cancellationToken = default)
    {
        var requestJson = JsonConvert.SerializeObject(request);

        var response = await Request($"data/{facilityId}/QueryPlan")
            .WithHeader("Content-Type", "application/json")
            .AllowAnyHttpStatus()
            .SendStringAsync(HttpMethod.Post, requestJson, cancellationToken: cancellationToken);

        return response.ResponseMessage.StatusCode;
    }

    public async Task<HttpStatusCode> DeleteQueryPlanAsync(string facilityId, string type, CancellationToken cancellationToken = default)
    {
        var response = await Request($"data/{facilityId}/QueryPlan")
            .SetQueryParam("type", type)
            .AllowAnyHttpStatus()
            .DeleteAsync(cancellationToken: cancellationToken);

        return response.ResponseMessage.StatusCode;
    }

    public async Task<(HttpStatusCode StatusCode, PagedConfigModel<DataAcquisitionLogApiModel>? Response)> SearchAcquisitionLogsAsync(
        string facilityId,
        string reportId,
        int pageSize = 5000,
        int pageNumber = 1,
        CancellationToken cancellationToken = default)
    {
        var response = await Request("data/acquisition-logs")
            .SetQueryParam("facilityId", facilityId)
            .SetQueryParam("reportId", reportId)
            .SetQueryParam("pageSize", pageSize)
            .SetQueryParam("pageNumber", pageNumber)
            .SetQueryParam("sortBy", "Id")
            .SetQueryParam("sortOrder", "Ascending")
            .AllowAnyHttpStatus()
            .GetAsync(cancellationToken: cancellationToken);

        return (response.ResponseMessage.StatusCode, await ReadJsonAsync<PagedConfigModel<DataAcquisitionLogApiModel>>(response));
    }

    public async Task<(HttpStatusCode StatusCode, DataAcquisitionLogStatusStatisticsApiModel? Response)> GetReportStatusCountsAsync(string reportId, CancellationToken cancellationToken = default)
    {
        var response = await Request($"data/acquisition-logs/report/{reportId}/status-counts")
            .AllowAnyHttpStatus()
            .GetAsync(cancellationToken: cancellationToken);

        return (response.ResponseMessage.StatusCode, await ReadJsonAsync<DataAcquisitionLogStatusStatisticsApiModel>(response));
    }

    public async Task<(HttpStatusCode StatusCode, PagedConfigModel<DataAcquisitionLogApiModel>? Response)> SearchDetailedAcquisitionLogsAsync(
        string facilityId,
        string reportId,
        int pageSize = 100,
        int pageNumber = 1,
        CancellationToken cancellationToken = default)
    {
        var response = await Request("data/acquisition-logs/detailed")
            .SetQueryParam("facilityId", facilityId)
            .SetQueryParam("reportId", reportId)
            .SetQueryParam("pageSize", pageSize)
            .SetQueryParam("pageNumber", pageNumber)
            .SetQueryParam("sortBy", "Id")
            .SetQueryParam("sortOrder", "Ascending")
            .AllowAnyHttpStatus()
            .GetAsync(cancellationToken: cancellationToken);

        return (response.ResponseMessage.StatusCode, await ReadJsonAsync<PagedConfigModel<DataAcquisitionLogApiModel>>(response));
    }

    public async Task<(HttpStatusCode StatusCode, List<string>? Response)> GetAcquiredResourceIdsForReportAsync(
        string facilityId,
        string reportId,
        CancellationToken cancellationToken = default)
    {
        var response = await Request($"data/acquisition-logs/report/{reportId}/acquired-resource-ids")
            .SetQueryParam("facilityId", facilityId)
            .AllowAnyHttpStatus()
            .GetAsync(cancellationToken: cancellationToken);

        return (response.ResponseMessage.StatusCode, await ReadJsonAsync<List<string>>(response));
    }
}
