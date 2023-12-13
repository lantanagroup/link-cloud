using LantanaGroup.Link.DataAcquisition.Application.Services.Interfaces;
using LantanaGroup.Link.DataAcquisition.Services.Interfaces;

namespace LantanaGroup.Link.DataAcquisition.Services;

public class TenantGrpcService : ITenantGrpcService
{
    private readonly ILogger<TenantGrpcService> _logger;
    private readonly Tenant.TenantClient _tenantClient;

    public TenantGrpcService(ILogger<TenantGrpcService> logger, Tenant.TenantClient tenantClient)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _tenantClient = tenantClient ?? throw new ArgumentNullException(nameof(tenantClient));
    }
}
