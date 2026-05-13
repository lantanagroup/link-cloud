using Flurl.Http;
using LantanaGroup.Link.Sdk.ApiClient;
using LantanaGroup.Link.Shared.Application.Extensions.Security;
using LantanaGroup.Link.Shared.Application.Interfaces.Services.Security.Token;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.Models.Integration.Normalization;
using LantanaGroup.Link.Shared.Application.Models.Responses;
using Microsoft.Extensions.Options;

namespace LantanaGroup.Link.Sdk.Clients;

public class NormalizationServiceClient : LinkApiClientBase, INormalizationServiceClient
{
    public NormalizationServiceClient(
        IOptions<ServiceRegistry> serviceRegistry,
        IOptions<BackendAuthenticationServiceExtension.LinkBearerServiceOptions> bearerOptions,
        IOptions<LinkTokenServiceSettings> tokenServiceSettings,
        ICreateSystemToken tokenService)
        : base(
            serviceRegistry.Value.NormalizationServiceApiUrl
                ?? throw new InvalidOperationException("Normalization service URL is not configured in ServiceRegistry."),
            bearerOptions, tokenServiceSettings, tokenService)
    { }

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
        DeleteOrIgnoreAsync(() => Request($"normalization/operations/facility/{facilityId}")
            .DeleteAsync(cancellationToken: cancellationToken));

    public Task<List<NormalizationOperationSequenceApiModel>> GetOperationSequencesAsync(
        string facilityId,
        CancellationToken cancellationToken = default) =>
        Request("normalization/OperationSequence")
            .SetQueryParam("facilityId", facilityId)
            .GetJsonAsync<List<NormalizationOperationSequenceApiModel>>(cancellationToken: cancellationToken);
}
