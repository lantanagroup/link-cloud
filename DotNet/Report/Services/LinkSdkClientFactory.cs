using LantanaGroup.Link.Shared.Application.Extensions.Security;
using LantanaGroup.Link.Shared.Application.Interfaces.Services.Security.Token;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.Models.Tenant;
using LantanaGroup.Link.Sdk.ApiClient;
using LantanaGroup.Link.Sdk.Clients;
using Microsoft.Extensions.Options;
using System.Net;

namespace LantanaGroup.Link.Report.Services;

public interface ILinkSdkClientFactory
{
    Task<FacilityModel?> GetFacilityConfigAsync(string facilityId, CancellationToken cancellationToken = default);
    Task<List<string>> GetAdmittedPatientIdsAsync(string facilityId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);
}

public class LinkSdkClientFactory : ILinkSdkClientFactory
{
    private readonly ServiceRegistry _serviceRegistry;
    private readonly IOptions<LinkTokenServiceSettings> _linkTokenServiceConfig;
    private readonly IOptions<BackendAuthenticationServiceExtension.LinkBearerServiceOptions> _linkBearerServiceOptions;
    private readonly ICreateSystemToken _createSystemToken;

    public LinkSdkClientFactory(
        IOptions<ServiceRegistry> serviceRegistry,
        IOptions<LinkTokenServiceSettings> linkTokenServiceConfig,
        IOptions<BackendAuthenticationServiceExtension.LinkBearerServiceOptions> linkBearerServiceOptions,
        ICreateSystemToken createSystemToken)
    {
        _serviceRegistry = serviceRegistry.Value;
        _linkTokenServiceConfig = linkTokenServiceConfig;
        _linkBearerServiceOptions = linkBearerServiceOptions;
        _createSystemToken = createSystemToken;
    }

    public async Task<FacilityModel?> GetFacilityConfigAsync(string facilityId, CancellationToken cancellationToken = default)
    {
        var baseUrl = _serviceRegistry.TenantServiceApiUrl
            ?? throw new InvalidOperationException("Tenant Service URL is missing.");

        var client = new FacilityServiceClient(await BuildSettingsAsync(baseUrl, cancellationToken));
        var (status, facility) = await client.GetDetailsAsync(facilityId, cancellationToken);

        return status == HttpStatusCode.OK ? facility : null;
    }

    public async Task<List<string>> GetAdmittedPatientIdsAsync(string facilityId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
    {
        var baseUrl = _serviceRegistry.CensusServiceApiUrl
            ?? throw new InvalidOperationException("Census Service URL is missing.");

        var client = new CensusServiceClient(await BuildSettingsAsync(baseUrl, cancellationToken));
        var (status, censusList) = await client.GetAdmittedPatientsAsync(facilityId, startDate, endDate, cancellationToken);

        if (status != HttpStatusCode.OK || censusList == null)
            return [];

        return censusList.Entry?
            .Where(e => !string.IsNullOrWhiteSpace(e.Item?.Reference))
            .Select(e => e.Item!.Reference!.Split('/').Last())
            .Distinct()
            .ToList() ?? [];
    }

    private async Task<ApiClientSettings> BuildSettingsAsync(string baseUrl, CancellationToken cancellationToken)
    {
        string? bearerToken = null;

        if (!_linkBearerServiceOptions.Value.AllowAnonymous)
        {
            if (_linkTokenServiceConfig.Value.SigningKey is null)
                throw new InvalidOperationException("Link Token Service Signing Key is missing.");

            bearerToken = await _createSystemToken.ExecuteAsync(_linkTokenServiceConfig.Value.SigningKey, 5);
        }

        return new ApiClientSettings
        {
            BaseUrl = baseUrl,
            BearerToken = bearerToken
        };
    }
}
