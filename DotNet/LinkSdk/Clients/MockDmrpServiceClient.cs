using Flurl.Http;
using LantanaGroup.Link.Sdk.ApiClient;
using LantanaGroup.Link.Shared.Application.Extensions.Security;
using LantanaGroup.Link.Shared.Application.Interfaces.Services.Security.Token;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.Models.Integration.MockDmrp;
using Microsoft.Extensions.Options;

namespace LantanaGroup.Link.Sdk.Clients;

/// <summary>
/// Calls the Mock DMRP API's support surface at an address the caller supplies.
/// </summary>
/// <remarks>
/// The address is the mock's root, not its <c>/api</c> prefix, because callers hold it in different
/// places: Tenant reaches the mock through <c>DMRP:Api:BaseUrl</c>, Automation through
/// <c>ServiceRegistry:MockDmrpApiUrl</c>.
/// </remarks>
public class MockDmrpServiceClient : LinkApiClientBase, IMockDmrpServiceClient
{
    private const string SupportRoute = "/api/mock-dmrp";

    /// <summary>
    /// Creates a client for the mock at <paramref name="baseUrl"/>, the mock's root address.
    /// </summary>
    public MockDmrpServiceClient(string baseUrl,
                                 IOptions<BackendAuthenticationServiceExtension.LinkBearerServiceOptions> bearerOptions,
                                 IOptions<LinkTokenServiceSettings> tokenServiceSettings,
                                 ICreateSystemToken tokenService)
        : base(baseUrl, bearerOptions, tokenServiceSettings, tokenService)
    {
    }

    /// <inheritdoc />
    public Task<LinkApiResponse<ServiceInformation>> GetInfoAsync(CancellationToken cancellationToken = default) =>
        SendAsync<ServiceInformation>(() => AnonymousRequest($"{SupportRoute}/info")
            .GetAsync(cancellationToken: cancellationToken));

    /// <inheritdoc />
    public Task<LinkApiResponse<MockDmrpEntryPage>> SearchEntriesAsync(string? facilityId = null,
        string? component = null, string? measure = null, int? reportingMonth = null,
        int? reportingYear = null, int pageSize = 10, int pageNumber = 1,
        CancellationToken cancellationToken = default) =>
        SendAsync<MockDmrpEntryPage>(() => Request($"{SupportRoute}/entries/search")
            .SetQueryParam("facilityId", facilityId)
            .SetQueryParam("component", component)
            .SetQueryParam("measure", measure)
            .SetQueryParam("reportingMonth", reportingMonth)
            .SetQueryParam("reportingYear", reportingYear)
            .SetQueryParam("pageSize", pageSize)
            .SetQueryParam("pageNumber", pageNumber)
            .GetAsync(cancellationToken: cancellationToken));

    /// <inheritdoc />
    public Task<LinkApiResponse<MockDmrpEntryResponse>> CreateEntryAsync(MockDmrpEntryRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        return SendAsync<MockDmrpEntryResponse>(() => Request($"{SupportRoute}/entries")
            .PostJsonAsync(request, cancellationToken: cancellationToken));
    }

    /// <inheritdoc />
    public Task<LinkApiResponse> DeleteEntryAsync(string id, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        return SendAsync(() => Request($"{SupportRoute}/entries")
            .AppendPathSegment(id, fullyEncode: true)
            .DeleteAsync(cancellationToken: cancellationToken));
    }

    /// <inheritdoc />
    public Task<LinkApiResponse> DeleteEntriesForFacilityAsync(string facilityId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(facilityId);

        return SendAsync(() => Request($"{SupportRoute}/facilities")
            .AppendPathSegment(facilityId, fullyEncode: true)
            .AppendPathSegment("entries")
            .DeleteAsync(cancellationToken: cancellationToken));
    }
}
