using System.Net;
using System.Text.Json;
using Flurl.Http;
using LantanaGroup.Link.Nhsn.App.Bff.Application.Interfaces.Infrastructure;
using LantanaGroup.Link.Nhsn.App.Bff.Application.Models.FacilityAdministration;
using LantanaGroup.Link.Nhsn.App.Bff.Application.Models.Onboarding;
using LantanaGroup.Link.Nhsn.App.Bff.Domain.Exceptions;
using LantanaGroup.Link.Sdk.ApiClient;
using LantanaGroup.Link.Shared.Application.Extensions.Security;
using LantanaGroup.Link.Shared.Application.Interfaces.Services.Security.Token;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using Microsoft.Extensions.Options;

namespace LantanaGroup.Link.Nhsn.App.Bff.Infrastructure.Link;

// Data Acquisition operations that don't fit LinkSdk's IDataAcquisitionServiceClient shape (see
// IDataAcquisitionRawClient), built on LinkApiClientBase — the same SDK base class LinkSdk's
// generated clients (e.g. DataAcquisitionServiceClient) use — for base-URL resolution and
// system-token injection, rather than assembling requests against a plain HttpClient by hand.
internal sealed class DataAcquisitionRawClient : LinkApiClientBase, IDataAcquisitionRawClient
{
    private const string ServiceName = "DataAcquisition";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private readonly ILogger<DataAcquisitionRawClient> _logger;

    public DataAcquisitionRawClient(
        IOptions<ServiceRegistry> serviceRegistry,
        IOptions<LinkTokenServiceSettings> linkTokenServiceConfig,
        IOptions<BackendAuthenticationServiceExtension.LinkBearerServiceOptions> linkBearerServiceOptions,
        ICreateSystemToken createSystemToken,
        ILogger<DataAcquisitionRawClient> logger)
        : base(
            serviceRegistry.Value.DataAcquisitionServiceApiUrl
                ?? throw new InvalidOperationException("DataAcquisition service URL is not configured in ServiceRegistry."),
            linkBearerServiceOptions, linkTokenServiceConfig, createSystemToken)
    {
        _logger = logger;
    }

    public async Task<FhirConnectionProbeResult> ValidateFhirServerConnectionAsync(string fhirServerBaseUrl, CancellationToken cancellationToken = default)
    {
        var response = await SendAsync(() => Request("data/connectionValidation/$validate")
            .SetQueryParam("fhirServerUrl", fhirServerBaseUrl)
            .GetAsync(cancellationToken: cancellationToken));

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("FHIR connection probe against {Url} returned HTTP {StatusCode}.", fhirServerBaseUrl, response.StatusCode);
            return new FhirConnectionProbeResult { IsConnected = false, ErrorMessage = response.RawBody };
        }

        try
        {
            return JsonSerializer.Deserialize<FhirConnectionProbeResult>(response.RawBody ?? string.Empty, JsonOptions)
                   ?? new FhirConnectionProbeResult { IsConnected = false };
        }
        catch (JsonException)
        {
            return new FhirConnectionProbeResult { IsConnected = false, ErrorMessage = response.RawBody };
        }
    }

    public async Task UpdateFhirQueryConfigurationAsync(UpdateFhirQueryConfigurationPayload payload, CancellationToken cancellationToken = default)
    {
        var response = await SendAsync(() => Request("data/fhirQueryConfiguration")
            .PutJsonAsync(payload, cancellationToken: cancellationToken));

        if (!response.IsSuccessStatusCode && response.StatusCode != (int)HttpStatusCode.NotModified)
        {
            throw new LinkServiceException(ServiceName, nameof(UpdateFhirQueryConfigurationAsync), response.StatusCode,
                response.TraceId, response.RawBody, response.RequestUrl);
        }
    }

    public async Task UpdateOrganizationLocationConfigurationAsync(string facilityId, UpdateOrganizationLocationConfigurationPayload payload, CancellationToken cancellationToken = default)
    {
        var response = await SendAsync(() => Request($"data/location-config/facility/{facilityId}")
            .PutJsonAsync(payload, cancellationToken: cancellationToken));

        if (!response.IsSuccessStatusCode && response.StatusCode != (int)HttpStatusCode.NotModified)
        {
            throw new LinkServiceException(ServiceName, nameof(UpdateOrganizationLocationConfigurationAsync), response.StatusCode,
                response.TraceId, response.RawBody, response.RequestUrl);
        }
    }
}
