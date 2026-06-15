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

    public Task<LinkApiResponse> PutMeasureDefinitionAsync(string bundleJson, CancellationToken cancellationToken = default) =>
        SendAsync(() => Request("measureeval/measure-definition").WithHeader("Content-Type", "application/json").PutStringAsync(bundleJson, cancellationToken: cancellationToken));

    public Task<LinkApiResponse<string>> GetMeasureDefinitionAsync(string measureId, CancellationToken cancellationToken = default) =>
        SendStringAsync(() => Request($"measureeval/measure-definition/{measureId}").GetAsync(cancellationToken: cancellationToken));

    public Task<LinkApiResponse<string>> GetAllMeasureDefinitionsAsync(CancellationToken cancellationToken = default) =>
        SendStringAsync(() => Request("measureeval/measure-definition").GetAsync(cancellationToken: cancellationToken));
}
