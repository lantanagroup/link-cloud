namespace LantanaGroup.Automation.Generation;

/// <summary>
/// Lightweight, serializable summary of a <see cref="GenerationManifest"/>
/// for display in the Automation UI. Stored as a domain snapshot per run.
/// </summary>
public sealed class GenerationManifestSnapshot
{
    public int PatientCount { get; init; }
    public int TotalResourceCount { get; init; }

    /// <summary>Ordered patient IDs.</summary>
    public IReadOnlyList<string> PatientIds { get; init; } = [];

    /// <summary>
    /// Patient IDs predicted to be submitted (qualify for at least one selected measure
    /// and are included by cohort/inpatient-pattern rules).
    /// </summary>
    public IReadOnlyList<string> ExpectedSubmittedPatientIds { get; init; } = [];

    /// <summary>Selected measure type names.</summary>
    public IReadOnlyList<string> SelectedMeasures { get; init; } = [];

    /// <summary>Pipeline measure ID strings (from MeasureLoader).</summary>
    public IReadOnlyList<string> MeasureIds { get; init; } = [];

    /// <summary>Resource types the query plan acquires.</summary>
    public IReadOnlyList<string> AcquiredResourceTypes { get; init; } = [];

    /// <summary>Resource types from Parameter queries only.</summary>
    public IReadOnlyList<string> ParameterQueryResourceTypes { get; init; } = [];

    /// <summary>Resource types referenced by CQL in the selected measures.</summary>
    public IReadOnlyList<string> CqlReferencedResourceTypes { get; init; } = [];

    /// <summary>Aggregate resource type → count across all patients (excludes shared infrastructure).</summary>
    public IReadOnlyDictionary<string, int> TotalCountsByType { get; init; } = new Dictionary<string, int>();

    /// <summary>Shared infrastructure resource type → count (e.g., Location, Organization, Practitioner).</summary>
    public IReadOnlyDictionary<string, int> SharedInfrastructureCountsByType { get; init; } = new Dictionary<string, int>();

    /// <summary>Per-patient resource type → count.</summary>
    public IReadOnlyDictionary<string, Dictionary<string, int>> ResourceCountsByPatient { get; init; }
        = new Dictionary<string, Dictionary<string, int>>();

    /// <summary>Per-patient eligibility summary: patient ID → list of qualifying measure names.</summary>
    public IReadOnlyDictionary<string, List<string>> PatientEligibility { get; init; }
        = new Dictionary<string, List<string>>();

    /// <summary>Per-patient scheduled inpatient pattern (enum name) used during generation.</summary>
    public IReadOnlyDictionary<string, string> PatientInpatientPatterns { get; init; }
        = new Dictionary<string, string>();

    /// <summary>
    /// Per-patient predicted ABS resource counts (generated ∩ query-plan-acquired ∩ CQL-referenced).
    /// This is the "our prediction" side of the Generated vs ABS comparison.
    /// </summary>
    public IReadOnlyDictionary<string, Dictionary<string, int>> ExpectedAbsCountsByPatient { get; init; }
        = new Dictionary<string, Dictionary<string, int>>();

    /// <summary>
    /// Aggregate predicted ABS resource type → count across all patients.
    /// </summary>
    public IReadOnlyDictionary<string, int> ExpectedAbsTotalCountsByType { get; init; }
        = new Dictionary<string, int>();

    /// <summary>
    /// Per-patient ABS template-cache key used to replay generated FHIR for download.
    /// </summary>
    public IReadOnlyDictionary<string, string> TemplateCacheKeyByPatient { get; init; }
        = new Dictionary<string, string>(StringComparer.Ordinal);
}
