using LantanaGroup.Link.Shared.Application.Interfaces.Services.Security.Token;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace LantanaGroup.Link.Automation.Link.Helpers;

public sealed class MockDmrpApiHelper
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptions<ServiceRegistry> _serviceRegistry;
    private readonly IOptions<LinkTokenServiceSettings> _tokenSettings;
    private readonly ICreateSystemToken _createSystemToken;

    public MockDmrpApiHelper(
        IHttpClientFactory httpClientFactory,
        IOptions<ServiceRegistry> serviceRegistry,
        IOptions<LinkTokenServiceSettings> tokenSettings,
        ICreateSystemToken createSystemToken)
    {
        _httpClientFactory = httpClientFactory;
        _serviceRegistry = serviceRegistry;
        _tokenSettings = tokenSettings;
        _createSystemToken = createSystemToken;
    }

    public async Task EnsureReachableAsync(
        string nhsnOrganizationId,
        CancellationToken cancellationToken = default)
    {
        var baseUrl = GetBaseUrl();
        var requestUrl =
            $"{baseUrl}/mock-dmrp/facilities/{Uri.EscapeDataString(nhsnOrganizationId)}/entries";

        using var request = await CreateAuthenticatedRequestAsync(
            HttpMethod.Get,
            requestUrl);

        using var client = _httpClientFactory.CreateClient();

        using var response = await client.SendAsync(
            request,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            throw new InvalidOperationException(
                $"MockDmrpApi reachability check failed with HTTP {(int)response.StatusCode}. " +
                $"Response: {body}");
        }
    }

    public async Task<MockDmrpEntryResponse> CreateEntryAsync(
        MockDmrpEntryRequest entry,
        CancellationToken cancellationToken = default)
    {
        var baseUrl = GetBaseUrl();
        var requestUrl = $"{baseUrl}/mock-dmrp/entries";

        using var request = await CreateAuthenticatedRequestAsync(
            HttpMethod.Post,
            requestUrl);

        request.Content = JsonContent.Create(entry);

        using var client = _httpClientFactory.CreateClient();

        using var response = await client.SendAsync(
            request,
            cancellationToken);

        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Failed to seed MockDmrpApi entry. " +
                $"HTTP {(int)response.StatusCode}. Response: {body}");
        }

        return await response.Content.ReadFromJsonAsync<MockDmrpEntryResponse>(
                   cancellationToken: cancellationToken)
               ?? throw new InvalidOperationException(
                   "MockDmrpApi returned an empty response when creating an entry.");
    }

    public async Task DeleteFacilityEntriesAsync(
        string nhsnOrganizationId,
        CancellationToken cancellationToken = default)
    {
        var baseUrl = GetBaseUrl();
        var requestUrl =
            $"{baseUrl}/mock-dmrp/facilities/{Uri.EscapeDataString(nhsnOrganizationId)}/entries";

        using var request = await CreateAuthenticatedRequestAsync(
            HttpMethod.Delete,
            requestUrl);

        using var client = _httpClientFactory.CreateClient();

        using var response = await client.SendAsync(
            request,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            throw new InvalidOperationException(
                $"Failed to clean up MockDmrpApi entries for NHSN organization " +
                $"'{nhsnOrganizationId}'. HTTP {(int)response.StatusCode}. Response: {body}");
        }
    }

    private string GetBaseUrl()
    {
        var baseUrl = _serviceRegistry.Value.MockDmrpApiApiUrl;

        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            throw new InvalidOperationException(
                "ServiceRegistry:MockDmrpApiUrl is not configured.");
        }

        return baseUrl.TrimEnd('/');
    }

    private async Task<HttpRequestMessage> CreateAuthenticatedRequestAsync(
        HttpMethod method,
        string requestUrl)
    {
        var signingKey = _tokenSettings.Value.SigningKey;

        if (string.IsNullOrWhiteSpace(signingKey))
        {
            throw new InvalidOperationException(
                "LinkTokenService:SigningKey is required to authenticate with MockDmrpApi.");
        }

        var token = await _createSystemToken.ExecuteAsync(signingKey, 5);

        var request = new HttpRequestMessage(method, requestUrl);
        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        return request;
    }
}

public sealed class MockDmrpEntryRequest
{
    public string FacilityId { get; init; } = string.Empty;

    public string Component { get; init; } = string.Empty;

    public string Measure { get; init; } = string.Empty;

    public int ReportingMonth { get; init; }

    public int ReportingYear { get; init; }

    public string IsReporting { get; init; } = "Y";
}

public sealed class MockDmrpEntryResponse
{
    public string Id { get; init; } = string.Empty;

    public string FacilityId { get; init; } = string.Empty;

    public string Component { get; init; } = string.Empty;

    public string Measure { get; init; } = string.Empty;

    public int ReportingMonth { get; init; }

    public int ReportingYear { get; init; }

    public string IsReporting { get; init; } = string.Empty;
}