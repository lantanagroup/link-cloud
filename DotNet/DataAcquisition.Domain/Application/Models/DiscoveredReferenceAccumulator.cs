namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Models;

/// <summary>
/// Per-primary-log-execution collector for reference resource ids discovered while
/// iterating FHIR bundle pages. Populated by <c>FhirApiService.ExecuteRead</c> /
/// <c>FhirApiService.ExecuteSearch</c> as references are extracted from bundles, and
/// drained once at the end of <c>PatientDataService.ExecuteLogRequest</c> by
/// <c>ReferenceResourceService.FetchAndPersistAsync</c>, which creates a single
/// same-phase reference <c>DataAcquisitionLog</c> per (correlation, resource type)
/// and appends the union of ids discovered across the primary log's FHIR queries.
/// <para>
/// Today population is single-threaded (the FHIR query / page loops in
/// <c>PatientDataService.ExecuteLogRequest</c> and <c>FhirApiService</c> await
/// sequentially), but <see cref="Add"/> takes an internal lock so the type stays
/// safe if any caller later parallelizes paging or fans resource types out with
/// <c>Task.WhenAll</c>. Cost is negligible compared to FHIR I/O.
/// </para>
/// </summary>
public sealed class DiscoveredReferenceAccumulator
{
    private readonly object _sync = new();

    /// <summary>
    /// Resource type (case-insensitive) -> distinct resource ids discovered for that type.
    /// Mutations must go through <see cref="Add"/>; direct callers should treat this as
    /// read-only and only enumerate it after population is complete (i.e. at drain time).
    /// </summary>
    public Dictionary<string, HashSet<string>> ByType { get; }
        = new(StringComparer.OrdinalIgnoreCase);

    public bool HasAny
    {
        get
        {
            lock (_sync)
            {
                return ByType.Any(kvp => kvp.Value.Count > 0);
            }
        }
    }

    public int Count
    {
        get
        {
            lock (_sync)
            {
                return ByType.Sum(kvp => kvp.Value.Count);
            }
        }
    }

    public void Add(string resourceType, string resourceId)
    {
        if (string.IsNullOrWhiteSpace(resourceType) || string.IsNullOrWhiteSpace(resourceId))
            return;

        lock (_sync)
        {
            if (!ByType.TryGetValue(resourceType, out var set))
            {
                set = new HashSet<string>(StringComparer.Ordinal);
                ByType[resourceType] = set;
            }

            set.Add(resourceId);
        }
    }
}
