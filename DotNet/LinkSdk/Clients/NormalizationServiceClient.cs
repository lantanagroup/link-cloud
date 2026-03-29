using Flurl.Http;
using LantanaGroup.Link.Shared.Application.Models.Integration.Normalization;
using LantanaGroup.Link.Shared.Application.Models.Responses;
using LantanaGroup.Link.Sdk.ApiClient;
using System.Net;

namespace LantanaGroup.Link.Sdk.Clients;

public sealed class NormalizationServiceClient : LinkApiClientBase
{
    public NormalizationServiceClient(ApiClientSettings settings)
        : base(settings)
    {
    }

    public async Task<(HttpStatusCode StatusCode, PagedConfigModel<NormalizationOperationApiModel>? Response)> SearchFacilityOperationsAsync(
        string facilityId,
        bool includeDisabled = true,
        int pageSize = 100,
        int pageNumber = 1,
        CancellationToken cancellationToken = default)
    {
        var response = await Request($"normalization/Operations/facility/{facilityId}")
            .SetQueryParam("includeDisabled", includeDisabled)
            .SetQueryParam("pageSize", pageSize)
            .SetQueryParam("pageNumber", pageNumber)
            .AllowAnyHttpStatus()
            .GetAsync(cancellationToken: cancellationToken);

        return (response.ResponseMessage.StatusCode, await ReadJsonAsync<PagedConfigModel<NormalizationOperationApiModel>>(response));
    }

    public async Task<HttpStatusCode> CreateOperationAsync(CreateNormalizationOperationRequestApiModel requestBody, CancellationToken cancellationToken = default)
    {
        var response = await Request("normalization/Operations")
            .AllowAnyHttpStatus()
            .PostJsonAsync(requestBody, cancellationToken: cancellationToken);

        return response.ResponseMessage.StatusCode;
    }

    public async Task<HttpStatusCode> DeleteFacilityOperationsAsync(string facilityId, CancellationToken cancellationToken = default)
    {
        var response = await Request($"normalization/operations/facility/{facilityId}")
            .AllowAnyHttpStatus()
            .DeleteAsync(cancellationToken: cancellationToken);

        return response.ResponseMessage.StatusCode;
    }

    public async Task<(HttpStatusCode StatusCode, List<NormalizationOperationSequenceApiModel>? Response)> GetOperationSequencesAsync(string facilityId, CancellationToken cancellationToken = default)
    {
        var response = await Request("normalization/OperationSequence")
            .SetQueryParam("facilityId", facilityId)
            .AllowAnyHttpStatus()
            .GetAsync(cancellationToken: cancellationToken);

        return (response.ResponseMessage.StatusCode, await ReadJsonAsync<List<NormalizationOperationSequenceApiModel>>(response));
    }
}
