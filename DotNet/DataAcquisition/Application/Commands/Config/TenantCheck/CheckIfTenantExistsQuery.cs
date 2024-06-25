using LantanaGroup.Link.Shared.Application.Interfaces.Services.Security.Token;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using MediatR;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;

namespace LantanaGroup.Link.DataAcquisition.Application.Commands.Config.TenantCheck;

public class CheckIfTenantExistsQuery : IRequest<bool>
{
    public string TenantId { get; set; }
}

public class CheckIfTenantExistsQueryHandler : IRequestHandler<CheckIfTenantExistsQuery, bool>
{
    private readonly HttpClient _httpClient;
    private readonly TenantServiceRegistration _tenantConfig;
    private readonly IOptions<LinkTokenServiceSettings> _linkTokenServiceConfig;
    private readonly ICreateSystemToken _createSystemToken;

    public CheckIfTenantExistsQueryHandler(HttpClient httpClient, IOptions<ServiceRegistry> serviceRegistry, IOptions<LinkTokenServiceSettings> linkTokenServiceConfig, ICreateSystemToken createSystemToken)
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
        _linkTokenServiceConfig = linkTokenServiceConfig ?? throw new ArgumentNullException(nameof(linkTokenServiceConfig));
        _createSystemToken = createSystemToken ?? throw new ArgumentNullException(nameof(createSystemToken));
    }

    public async Task<bool> Handle(CheckIfTenantExistsQuery request, CancellationToken cancellationToken)
    {
        if (!_tenantConfig.CheckIfTenantExists)
            return true;

        //TODO: add method to get key that includes looking at redis for future use case
        if (_linkTokenServiceConfig.Value.SigningKey is null)
            throw new Exception("Link Token Service Signing Key is missing.");

        //get link token
        var token = await _createSystemToken.ExecuteAsync(_linkTokenServiceConfig.Value.SigningKey, 2);
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var url = $"{_tenantConfig.TenantServiceApiUrl.TrimEnd('/')}/{_tenantConfig.GetTenantRelativeEndpoint.TrimEnd('/').TrimStart('/')}/{request.TenantId}";
        var response = await _httpClient.GetAsync(url, cancellationToken);
        return response.IsSuccessStatusCode;
    }
}

