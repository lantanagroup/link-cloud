using LantanaGroup.Link.Sdk.ApiClient;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Integration.MockDmrp;

namespace LantanaGroup.Link.Sdk.Clients;

/// <summary>
/// The Mock DMRP API's support surface (<c>/api/mock-dmrp</c>): the routes that seed and inspect the
/// enrollment the mock's contract endpoints serve.
/// </summary>
/// <remarks>
/// Separate from <see cref="IDmrpServiceClient"/>, which calls Link's own DMRP routes on the Tenant
/// service. This one calls the stand-in for the third party, at whatever address the caller gives it.
/// </remarks>
public interface IMockDmrpServiceClient
{
    /// <summary>
    /// Gets the service's build information. Sent without a bearer token.
    /// </summary>
    /// <remarks>
    /// Answers even while the mock is disabled. The caller compares <see cref="ServiceInformation.ServiceName"/>
    /// to decide whether the host is the mock at all before sending it anything authenticated.
    /// </remarks>
    Task<LinkApiResponse<ServiceInformation>> GetInfoAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches entries. Every filter is optional, and a filter left null is not sent.
    /// </summary>
    /// <param name="pageSize">
    /// Entries per page, 1-100.
    /// </param>
    /// <param name="pageNumber">
    /// One-based.
    /// </param>
    Task<LinkApiResponse<MockDmrpEntryPage>> SearchEntriesAsync(string? facilityId = null,
        string? component = null, string? measure = null, int? reportingMonth = null,
        int? reportingYear = null, int pageSize = 10, int pageNumber = 1,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates an entry. Answers 409 when the facility already has one for that component, measure
    /// and period.
    /// </summary>
    Task<LinkApiResponse<MockDmrpEntryResponse>> CreateEntryAsync(MockDmrpEntryRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes one entry by its identifier.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// <paramref name="id"/> is blank. Refused rather than sent, because the route without an id
    /// deletes every entry the mock holds.
    /// </exception>
    Task<LinkApiResponse> DeleteEntryAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes every entry belonging to one facility. Idempotent.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// <paramref name="facilityId"/> is blank.
    /// </exception>
    Task<LinkApiResponse> DeleteEntriesForFacilityAsync(string facilityId,
        CancellationToken cancellationToken = default);
}
