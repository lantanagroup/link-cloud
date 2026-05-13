using Flurl.Http;
using LantanaGroup.Link.Sdk.ApiClient;
using LantanaGroup.Link.Shared.Application.Extensions.Security;
using LantanaGroup.Link.Shared.Application.Interfaces.Services.Security.Token;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using Microsoft.Extensions.Options;

namespace LantanaGroup.Link.Sdk.Clients;

public class MeasureEvalServiceClient : LinkApiClientBase, IMeasureEvalServiceClient
{
    public MeasureEvalServiceClient(
        IOptions<ServiceRegistry> serviceRegistry,
        IOptions<BackendAuthenticationServiceExtension.LinkBearerServiceOptions> bearerOptions,
        IOptions<LinkTokenServiceSettings> tokenServiceSettings,
        ICreateSystemToken tokenService)
        : base(
            serviceRegistry.Value.MeasureServiceApiUrl
                ?? throw new InvalidOperationException("MeasureEval service URL is not configured in ServiceRegistry."),
            bearerOptions, tokenServiceSettings, tokenService)
    { }

    public Task PutMeasureDefinitionAsync(string bundleJson, CancellationToken cancellationToken = default) =>
        Request("measureeval/measure-definition").WithHeader("Content-Type", "application/json").PutStringAsync(bundleJson, cancellationToken: cancellationToken);

    public Task<string?> GetMeasureDefinitionAsync(string measureId, CancellationToken cancellationToken = default) =>
        GetOrDefaultAsync(() => Request($"measureeval/measure-definition/{measureId}").GetStringAsync(cancellationToken: cancellationToken));
}
