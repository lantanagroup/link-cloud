using LantanaGroup.Link.DataAcquisition.Application.Settings;
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
    private readonly TenantConfig _tenantConfig;

    public CheckIfTenantExistsQueryHandler(HttpClient httpClient, IOptions<TenantConfig> tenantConfig)
    {
        _httpClient = httpClient;
        _tenantConfig = tenantConfig.Value;
    }

    public async Task<bool> Handle(CheckIfTenantExistsQuery request, CancellationToken cancellationToken)
    {
        var url = $"{_tenantConfig.TenantServiceBaseUrl}{_tenantConfig.TenantService_GetTenantRelativeUrl}/{request.TenantId}";
        var response = await _httpClient.GetAsync(url, cancellationToken);
        return response.IsSuccessStatusCode;
    }
}

