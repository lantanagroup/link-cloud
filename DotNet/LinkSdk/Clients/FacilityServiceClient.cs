using Flurl.Http;
using LantanaGroup.Link.Sdk.ApiClient;
using LantanaGroup.Link.Shared.Application.Extensions.Security;
using LantanaGroup.Link.Shared.Application.Interfaces.Services.Security.Token;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.Models.Tenant;
using Microsoft.Extensions.Options;

namespace LantanaGroup.Link.Sdk.Clients;

public class FacilityServiceClient : LinkApiClientBase, IFacilityServiceClient
{
    private readonly IOptions<ServiceRegistry> _serviceRegistry;

    public FacilityServiceClient(
        IOptions<ServiceRegistry> serviceRegistry,
        IOptions<BackendAuthenticationServiceExtension.LinkBearerServiceOptions> bearerOptions,
        IOptions<LinkTokenServiceSettings> tokenServiceSettings,
        ICreateSystemToken tokenService)
        : base(
            serviceRegistry.Value.TenantServiceApiUrl
                ?? throw new InvalidOperationException("Tenant service URL is not configured in ServiceRegistry."),
            bearerOptions, tokenServiceSettings, tokenService)
    {
        _serviceRegistry = serviceRegistry;
    }

    public Task<FacilityModel> CreateAsync(
        FacilityModel request,
        CancellationToken cancellationToken = default) =>
        Request("/Facility")
            .PostJsonAsync(request, cancellationToken: cancellationToken)
            .ReceiveJson<FacilityModel>();

    public Task<FacilityModel?> GetAsync(
        string facilityId,
        CancellationToken cancellationToken = default) =>
        GetOrDefaultAsync(() => Request($"/Facility/{facilityId}")
            .GetJsonAsync<FacilityModel>(cancellationToken: cancellationToken));

    public Task<bool> CheckFacilityExistsAsync(
        string facilityId,
        CancellationToken cancellationToken = default)
    {
        if (_serviceRegistry.Value.TenantService?.CheckIfTenantExists != true)
            return Task.FromResult(true);

        return ExistsAsync(() => Request($"/Facility/{facilityId}")
            .GetAsync(cancellationToken: cancellationToken));
    }

    public Task DeleteAsync(
        string facilityId,
        CancellationToken cancellationToken = default) =>
        DeleteOrIgnoreAsync(() => Request($"/Facility/{facilityId}")
            .DeleteAsync(cancellationToken: cancellationToken));
}
