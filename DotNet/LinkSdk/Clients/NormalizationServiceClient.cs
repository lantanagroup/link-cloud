using Flurl.Http;
using LantanaGroup.Link.Shared.Application.Models.Integration.Normalization;
using LantanaGroup.Link.Shared.Application.Models.Responses;
using LantanaGroup.Link.Sdk.ApiClient;

namespace LantanaGroup.Link.Sdk.Clients;

public sealed class NormalizationServiceClient : LinkApiClientBase
{
    public NormalizationServiceClient(ApiClientSettings settings)
        : base(settings)
    {
    }

    public Task<PagedConfigModel<NormalizationOperationApiModel>> SearchFacilityOperationsAsync(
        string facilityId,
        bool includeDisabled = true,
        int pageSize = 100,
        int pageNumber = 1,
        CancellationToken cancellationToken = default) =>
        Request($"normalization/Operations/facility/{facilityId}")
            .SetQueryParam("includeDisabled", includeDisabled)
            .SetQueryParam("pageSize", pageSize)
            .SetQueryParam("pageNumber", pageNumber)
            .GetJsonAsync<PagedConfigModel<NormalizationOperationApiModel>>(cancellationToken: cancellationToken);

    public Task CreateOperationAsync(
        CreateNormalizationOperationRequestApiModel requestBody,
        CancellationToken cancellationToken = default) =>
        Request("normalization/Operations")
            .PostJsonAsync(requestBody, cancellationToken: cancellationToken);

    public Task DeleteFacilityOperationsAsync(
        string facilityId,
        CancellationToken cancellationToken = default) =>
        Request($"normalization/operations/facility/{facilityId}")
            .DeleteAsync(cancellationToken: cancellationToken);

    public Task<List<NormalizationOperationSequenceApiModel>> GetOperationSequencesAsync(
        string facilityId,
        CancellationToken cancellationToken = default) =>
        Request("normalization/OperationSequence")
            .SetQueryParam("facilityId", facilityId)
            .GetJsonAsync<List<NormalizationOperationSequenceApiModel>>(cancellationToken: cancellationToken);
}
