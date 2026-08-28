using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using LantanaGroup.Link.Nhsn.App.Bff.Application.Interfaces.Infrastructure;
using LantanaGroup.Link.Nhsn.App.Bff.Application.Models.FacilityAdministration;
using LantanaGroup.Link.Shared.Application.Extensions.Security;
using LantanaGroup.Link.Shared.Application.Interfaces.Services.Security.Token;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using Microsoft.Extensions.Options;

namespace LantanaGroup.Link.Nhsn.App.Bff.Infrastructure.Link;

// IDataAcquisitionRawClient over a plain HttpClient. Mirrors the pattern DotNet/Shared's
// TenantApiService already uses for Tenant — IHttpClientFactory.CreateClient(), a signed system
// token from ICreateSystemToken, no generated SDK involved — kept local to this BFF rather than
// extending LinkSdk.
internal sealed class DataAcquisitionRawClient : IDataAcquisitionRawClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptions<ServiceRegistry> _serviceRegistry;
    private readonly IOptions<LinkTokenServiceSettings> _linkTokenServiceConfig;
    private readonly IOptions<BackendAuthenticationServiceExtension.LinkBearerServiceOptions> _linkBearerServiceOptions;
    private readonly ICreateSystemToken _createSystemToken;
    private readonly ILogger<DataAcquisitionRawClient> _logger;

    public DataAcquisitionRawClient(
        IHttpClientFactory httpClientFactory,
        IOptions<ServiceRegistry> serviceRegistry,
        IOptions<LinkTokenServiceSettings> linkTokenServiceConfig,
        IOptions<BackendAuthenticationServiceExtension.LinkBearerServiceOptions> linkBearerServiceOptions,
        ICreateSystemToken createSystemToken,
        ILogger<DataAcquisitionRawClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _serviceRegistry = serviceRegistry;
        _linkTokenServiceConfig = linkTokenServiceConfig;
        _linkBearerServiceOptions = linkBearerServiceOptions;
        _createSystemToken = createSystemToken;
        _logger = logger;
    }

    public async Task<FhirConnectionProbeResult> ValidateFhirServerConnectionAsync(string fhirServerBaseUrl, CancellationToken cancellationToken = default)
    {
        var client = await CreateAuthenticatedClientAsync(cancellationToken);
        var uri = $"{RequireBaseUrl()}/data/connectionValidation/$validate?fhirServerUrl={Uri.EscapeDataString(fhirServerBaseUrl)}";

        var response = await client.GetAsync(uri, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("FHIR connection probe against {Url} returned HTTP {StatusCode}.", fhirServerBaseUrl, response.StatusCode);
            return new FhirConnectionProbeResult { IsConnected = false, ErrorMessage = body };
        }

        try
        {
            return JsonSerializer.Deserialize<FhirConnectionProbeResult>(body, JsonOptions)
                   ?? new FhirConnectionProbeResult { IsConnected = false };
        }
        catch (JsonException)
        {
            return new FhirConnectionProbeResult { IsConnected = false, ErrorMessage = body };
        }
    }

    public async Task UpdateFhirQueryConfigurationAsync(UpdateFhirQueryConfigurationPayload payload, CancellationToken cancellationToken = default)
    {
        var client = await CreateAuthenticatedClientAsync(cancellationToken);
        var uri = $"{RequireBaseUrl()}/data/fhirQueryConfiguration";

        var response = await client.PutAsJsonAsync(uri, payload, JsonOptions, cancellationToken);

        if (!response.IsSuccessStatusCode && response.StatusCode != HttpStatusCode.NotModified)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException(
                $"Unable to update FHIR query configuration in Data Acquisition. Data Acquisition returned HTTP {(int)response.StatusCode}: {body}");
        }
    }

    private async Task<HttpClient> CreateAuthenticatedClientAsync(CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient();

        if (!_linkBearerServiceOptions.Value.AllowAnonymous)
        {
            var signingKey = _linkTokenServiceConfig.Value.SigningKey
                ?? throw new InvalidOperationException("Link Token Service Signing Key is missing.");
            var token = await _createSystemToken.ExecuteAsync(signingKey, 5);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return client;
    }

    private string RequireBaseUrl() =>
        _serviceRegistry.Value.DataAcquisitionServiceApiUrl
        ?? throw new InvalidOperationException("DataAcquisition service URL is not configured in ServiceRegistry.");
}
