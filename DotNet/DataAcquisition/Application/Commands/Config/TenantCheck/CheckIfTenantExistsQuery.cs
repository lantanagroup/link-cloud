using LantanaGroup.Link.Shared.Application.Models.Configs;
using MediatR;
using Microsoft.Extensions.Options;

namespace LantanaGroup.Link.DataAcquisition.Application.Commands.Config.TenantCheck;

public class CheckIfTenantExistsQuery : IRequest<bool>
{
    public string TenantId { get; set; }
}

public class CheckIfTenantExistsQueryHandler : IRequestHandler<CheckIfTenantExistsQuery, bool>
{
    private readonly HttpClient _httpClient;
    private readonly TenantServiceRegistration _tenantConfig;

    public CheckIfTenantExistsQueryHandler(HttpClient httpClient, IOptions<ServiceRegistry> serviceRegistry)
    {
        _httpClient = httpClient;

        if (serviceRegistry.Value is null)
            throw new ArgumentNullException(nameof(serviceRegistry));
        else if (serviceRegistry.Value.TenantService is null)
            throw new ArgumentNullException(nameof(serviceRegistry.Value.TenantService));
        else if (serviceRegistry.Value.TenantService.TenantServiceUrl is null)
            throw new ArgumentNullException(nameof(serviceRegistry.Value.TenantService.TenantServiceUrl));
        else if (serviceRegistry.Value.TenantService.GetTenantRelativeEndpoint is null)
            throw new ArgumentNullException(nameof(serviceRegistry.Value.TenantService.GetTenantRelativeEndpoint));

        _tenantConfig = serviceRegistry.Value.TenantService;
    }

    public async Task<bool> Handle(CheckIfTenantExistsQuery request, CancellationToken cancellationToken)
    {
        if (!_tenantConfig.CheckIfTenantExists)
            return true;

        var url = $"{_tenantConfig.TenantServiceUrl.TrimEnd('/')}/{_tenantConfig.GetTenantRelativeEndpoint.TrimEnd('/').TrimStart('/')}/{request.TenantId}";
        var response = await _httpClient.GetAsync(url, cancellationToken);
        return response.IsSuccessStatusCode;
    }
}

