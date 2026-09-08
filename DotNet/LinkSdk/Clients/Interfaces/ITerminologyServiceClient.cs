using LantanaGroup.Link.Sdk.ApiClient;

namespace LantanaGroup.Link.Sdk.Clients;

/// <summary>
/// Client for the Link Terminology service FHIR terminology operations. Bodies are returned as raw
/// JSON strings (FHIR <c>ValueSet</c> / <c>Bundle</c> / <c>Parameters</c> resources) so the SDK does
/// not take a dependency on the Hl7.Fhir model.
/// </summary>
public interface ITerminologyServiceClient
{
    /// <summary>
    /// Expands a ValueSet by id or canonical url: <c>GET /api/terminology/fhir/ValueSet/$expand</c>
    /// (or <c>/ValueSet/{id}/$expand</c> when <paramref name="id"/> is supplied). Used for
    /// encounter-code autocomplete.
    /// </summary>
    Task<LinkApiResponse<string>> ExpandValueSetAsync(string? id = null, string? url = null, string? date = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves ValueSet resources, optionally filtered by canonical url:
    /// <c>GET /api/terminology/fhir/ValueSet</c>.
    /// </summary>
    Task<LinkApiResponse<string>> GetValueSetsAsync(string? url = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Looks up the details of a code in a CodeSystem:
    /// <c>GET /api/terminology/fhir/CodeSystem/$lookup</c> (or <c>/CodeSystem/{id}/$lookup</c> when
    /// <paramref name="id"/> is supplied). Used for code-detail lookup.
    /// </summary>
    Task<LinkApiResponse<string>> LookupCodeInCodeSystemAsync(string? system = null, string? code = null, string? version = null, string? id = null, CancellationToken cancellationToken = default);
}
