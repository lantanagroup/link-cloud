using Flurl.Http;
using LantanaGroup.Link.Sdk.ApiClient;
using LantanaGroup.Link.Shared.Application.Extensions.Security;
using LantanaGroup.Link.Shared.Application.Interfaces.Services.Security.Token;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.Models.Responses;
using LantanaGroup.Link.Shared.Application.Models.Terminology;
using Microsoft.Extensions.Options;

namespace LantanaGroup.Link.Sdk.Clients;

public class TerminologyServiceClient : LinkApiClientBase, ITerminologyServiceClient
{
    public TerminologyServiceClient(
        IOptions<ServiceRegistry> serviceRegistry,
        IOptions<BackendAuthenticationServiceExtension.LinkBearerServiceOptions> bearerOptions,
        IOptions<LinkTokenServiceSettings> tokenServiceSettings,
        ICreateSystemToken tokenService)
        : base(
            serviceRegistry.Value.TerminologyServiceApiUrl
                ?? throw new InvalidOperationException("Terminology service URL is not configured in ServiceRegistry."),
            bearerOptions, tokenServiceSettings, tokenService)
    { }

    /// <inheritdoc />
    // Flurl drops a null query parameter, so an unsupplied filter is omitted from the URL rather than
    // sent empty -- which matters here, because the service rejects a blank codeSystem or valueSet
    // instead of treating it as absent.
    public Task<LinkApiResponse<PagedConfigModel<TerminologyCodeModel>>> SearchCodesAsync(
        string? search = null,
        string? codeSystem = null,
        string? valueSet = null,
        string? version = null,
        bool excludeInactive = false,
        int pageNumber = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default) =>
        SendAsync<PagedConfigModel<TerminologyCodeModel>>(() => Request("terminology/codes")
            .SetQueryParam("search", search)
            .SetQueryParam("codeSystem", codeSystem)
            .SetQueryParam("valueSet", valueSet)
            .SetQueryParam("version", version)
            .SetQueryParam("excludeInactive", excludeInactive)
            .SetQueryParam("pageNumber", pageNumber)
            .SetQueryParam("pageSize", pageSize)
            .GetAsync(cancellationToken: cancellationToken));
}
