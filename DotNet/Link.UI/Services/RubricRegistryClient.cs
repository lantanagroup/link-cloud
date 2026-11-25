using Flurl.Http;
using LantanaGroup.Link.Sdk.ApiClient;
using LantanaGroup.Link.Shared.Application.Interfaces.Services.Security.Token;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using Microsoft.Extensions.Options;
using Security = LantanaGroup.Link.Shared.Application.Extensions.Security;

namespace Link.UI.Services;

/// <summary>
/// Validation rubric registry. Calls use the same system token as the other Validation client.
/// The signed-in administrator is not forwarded. Publish, retire, and delete are recorded as that system identity.
/// </summary>
public sealed class RubricRegistryClient : LinkApiClientBase, IRubricRegistry
{
    public RubricRegistryClient(
        IOptions<ServiceRegistry> serviceRegistry,
        IOptions<Security.BackendAuthenticationServiceExtension.LinkBearerServiceOptions> bearerOptions,
        IOptions<LinkTokenServiceSettings> tokenServiceSettings,
        ICreateSystemToken tokenService)
        : base(
            serviceRegistry.Value.ValidationServiceApiUrl
                ?? throw new InvalidOperationException("Validation service URL is not configured in ServiceRegistry."),
            bearerOptions, tokenServiceSettings, tokenService)
    {
    }

    public Task<LinkApiResponse<string>> SendAsync(
        HttpMethod method,
        string relativePath,
        string? body,
        string? contentType,
        IReadOnlyDictionary<string, string>? headers,
        CancellationToken cancellationToken) =>
        SendStringAsync(() =>
        {
            IFlurlRequest request = Request(relativePath);
            if (headers is not null)
            {
                foreach (var header in headers)
                    request = request.WithHeader(header.Key, header.Value);
            }

            if (method == HttpMethod.Get)
                return request.GetAsync(cancellationToken: cancellationToken);
            if (method == HttpMethod.Delete)
                return request.DeleteAsync(cancellationToken: cancellationToken);
            if (body is null)
                return request.PostAsync(cancellationToken: cancellationToken);
            return request.WithHeader("Content-Type", contentType ?? "application/json")
                .PostStringAsync(body, cancellationToken: cancellationToken);
        });
}
