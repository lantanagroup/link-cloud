using Flurl.Http;
using LantanaGroup.Link.Sdk.ApiClient;
using LantanaGroup.Link.Shared.Application.Extensions.Security;
using LantanaGroup.Link.Shared.Application.Interfaces.Services.Security.Token;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.Models.Integration.QueryDispatch;
using Microsoft.Extensions.Options;

namespace LantanaGroup.Link.Sdk.Clients;

public class QueryDispatchServiceClient : LinkApiClientBase, IQueryDispatchServiceClient
{
    public QueryDispatchServiceClient(
        IOptions<ServiceRegistry> serviceRegistry,
        IOptions<BackendAuthenticationServiceExtension.LinkBearerServiceOptions> bearerOptions,
        IOptions<LinkTokenServiceSettings> tokenServiceSettings,
        ICreateSystemToken tokenService)
        : base(
            serviceRegistry.Value.QueryDispatchServiceApiUrl
                ?? throw new InvalidOperationException("QueryDispatch service URL is not configured in ServiceRegistry."),
            bearerOptions, tokenServiceSettings, tokenService)
    { }

    public Task<LinkApiResponse<QueryDispatchConfigurationApiModel>> GetConfigurationAsync(
        string facilityId,
        CancellationToken cancellationToken = default) =>
        SendAsync<QueryDispatchConfigurationApiModel>(() => Request($"querydispatch/configuration/facility/{facilityId}")
            .GetAsync(cancellationToken: cancellationToken));

    public Task<LinkApiResponse> CreateQueryDispatchConfigurationAsync(
        QueryDispatchConfigurationApiModel configuration,
        CancellationToken cancellationToken = default) =>
        SendAsync(() => Request("querydispatch/configuration")
            .PostJsonAsync(configuration, cancellationToken: cancellationToken));

    public Task<LinkApiResponse> UpsertQueryDispatchConfigurationAsync(
        string facilityId,
        QueryDispatchConfigurationApiModel configuration,
        CancellationToken cancellationToken = default) =>
        SendAsync(() => Request($"querydispatch/configuration/facility/{facilityId}")
            .PutJsonAsync(configuration, cancellationToken: cancellationToken));

    public Task<LinkApiResponse> DeleteQueryDispatchConfigurationAsync(
        string facilityId,
        CancellationToken cancellationToken = default) =>
        SendAsync(() => Request($"querydispatch/configuration/facility/{facilityId}")
            .DeleteAsync(cancellationToken: cancellationToken));
}
