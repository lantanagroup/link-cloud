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

    public Task UpsertQueryDispatchConfigurationAsync(
        string facilityId,
        QueryDispatchConfigurationApiModel configuration,
        CancellationToken cancellationToken = default) =>
        Request($"querydispatch/configuration/facility/{facilityId}")
            .PutJsonAsync(configuration, cancellationToken: cancellationToken);

    public Task DeleteQueryDispatchConfigurationAsync(
        string facilityId,
        CancellationToken cancellationToken = default) =>
        DeleteOrIgnoreAsync(() => Request($"querydispatch/configuration/facility/{facilityId}")
            .DeleteAsync(cancellationToken: cancellationToken));
}
