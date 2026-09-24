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

    public Task<LinkApiResponse<string>> ExpandValueSetAsync(
        string? id = null,
        string? url = null,
        string? date = null,
        CancellationToken cancellationToken = default)
    {
        var request = string.IsNullOrWhiteSpace(id)
            ? Request("terminology/fhir/ValueSet/$expand")
            : Request($"terminology/fhir/ValueSet/{id}/$expand");
        if (!string.IsNullOrWhiteSpace(url)) request = request.SetQueryParam("url", url);
        if (!string.IsNullOrWhiteSpace(date)) request = request.SetQueryParam("date", date);
        return SendStringAsync(() => request.GetAsync(cancellationToken: cancellationToken));
    }

    public Task<LinkApiResponse<string>> GetValueSetsAsync(
        string? url = null,
        CancellationToken cancellationToken = default)
    {
        var request = Request("terminology/fhir/ValueSet");
        if (!string.IsNullOrWhiteSpace(url)) request = request.SetQueryParam("url", url);
        return SendStringAsync(() => request.GetAsync(cancellationToken: cancellationToken));
    }

    public Task<LinkApiResponse<string>> LookupCodeInCodeSystemAsync(
        string? system = null,
        string? code = null,
        string? version = null,
        string? id = null,
        CancellationToken cancellationToken = default)
    {
        var request = string.IsNullOrWhiteSpace(id)
            ? Request("terminology/fhir/CodeSystem/$lookup")
            : Request($"terminology/fhir/CodeSystem/{id}/$lookup");
        if (!string.IsNullOrWhiteSpace(system)) request = request.SetQueryParam("system", system);
        if (!string.IsNullOrWhiteSpace(code)) request = request.SetQueryParam("code", code);
        if (!string.IsNullOrWhiteSpace(version)) request = request.SetQueryParam("version", version);
        return SendStringAsync(() => request.GetAsync(cancellationToken: cancellationToken));
    }

    public Task<LinkApiResponse<string>> LookupCodeInCodeSystemWithParametersAsync(
        string parametersJson,
        string? system = null,
        string? code = null,
        string? version = null,
        string? id = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(parametersJson);
        var request = string.IsNullOrWhiteSpace(id)
            ? Request("terminology/fhir/CodeSystem/$lookup")
            : Request($"terminology/fhir/CodeSystem/{id}/$lookup");
        if (!string.IsNullOrWhiteSpace(system)) request = request.SetQueryParam("system", system);
        if (!string.IsNullOrWhiteSpace(code)) request = request.SetQueryParam("code", code);
        if (!string.IsNullOrWhiteSpace(version)) request = request.SetQueryParam("version", version);
        return SendStringAsync(() => request
            .WithHeader("Content-Type", "application/fhir+json")
            .SendStringAsync(HttpMethod.Post, parametersJson, cancellationToken: cancellationToken));
    }
}
