namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Models;

/// <summary>
/// Per-primary-log-execution collector for reference resource ids discovered while
/// iterating FHIR bundle pages. Populated by <c>FhirApiService.ExecuteRead</c> /
/// <c>FhirApiService.ExecuteSearch</c> as references are extracted from bundles, and
/// drained once at the end of <c>PatientDataService.ExecuteLogRequest</c> by
/// <c>ReferenceResourceService.FetchAndPersistAsync</c>, which creates a single
/// same-phase reference <c>DataAcquisitionLog</c> per (correlation, resource type)
/// and appends the union of ids discovered across the primary log's FHIR queries.
/// </summary>
public sealed class DiscoveredReferenceAccumulator
{
    /// <summary>
    /// Resource type (case-insensitive) -> distinct resource ids discovered for that type.
    /// </summary>
    public Dictionary<string, HashSet<string>> ByType { get; }
        = new(StringComparer.OrdinalIgnoreCase);

    public bool HasAny => ByType.Any(kvp => kvp.Value.Count > 0);

    public void Add(string resourceType, string resourceId)
    {
        if (string.IsNullOrWhiteSpace(resourceType) || string.IsNullOrWhiteSpace(resourceId))
            return;

        if (!ByType.TryGetValue(resourceType, out var set))
        {
            set = new HashSet<string>(StringComparer.Ordinal);
            ByType[resourceType] = set;
        }

        set.Add(resourceId);
    }
}
