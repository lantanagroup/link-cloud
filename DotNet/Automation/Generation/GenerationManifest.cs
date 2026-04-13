using System.Text.Json;

namespace LantanaGroup.Automation.Generation;

/// <summary>
/// A complete, concrete manifest of everything that was generated. Built once at generation
/// time and passed through to every validator so they can assert expected pipeline output
/// against known inputs - without interrogating the pipeline's own data (self-affirming)
/// or relying on brittle baselines.
/// </summary>
public sealed class GenerationManifest
{
    /// <summary>
    /// Resource types that are pipeline-derived (not from generation input).
    /// These should never be compared between generated and actual data.
    /// </summary>
    private static readonly HashSet<string> PipelineDerivedTypes =
        new(StringComparer.OrdinalIgnoreCase) { "MeasureReport", "OperationOutcome" };

    /// <summary>Ordered patient IDs, same order as the profiles.</summary>
    public IReadOnlyList<string> PatientIds { get; init; } = [];

    /// <summary>Ordered patient profiles (eligibility, resource count, scenario).</summary>
    public IReadOnlyList<PatientProfile> Profiles { get; init; } = [];

    /// <summary>Selected measure enum types used during generation.</summary>
    public IReadOnlyList<ProfiledMeasureType> SelectedMeasures { get; init; } = [];

    /// <summary>
    /// Pipeline measure ID strings (from <c>MeasureLoader.MeasureIds</c>), same order
    /// as <see cref="SelectedMeasures"/>. Set after measure loading.
    /// </summary>
    public IReadOnlyList<string> MeasureIds { get; set; } = [];

    /// <summary>
    /// The set of FHIR resource types that the query plan actually acquires.
    /// Only resources of these types (plus Patient) will flow through the pipeline
    /// into Report/ABS. Set from <c>QueryPlanDefaults.GetAcquiredResourceTypes()</c>.
    /// When empty, resource type filtering is disabled (backward-compatible).
    /// </summary>
    public HashSet<string> AcquiredResourceTypes { get; set; } = [];

    /// <summary>
    /// The subset of <see cref="AcquiredResourceTypes"/> that come from <b>Parameter</b>
    /// queries - direct, patient-scoped searches whose results are deterministic.
    /// Reference-query types (Location, Medication, Device, Specimen, etc.) are
    /// in <see cref="AcquiredResourceTypes"/> but not here.
    /// Set from <c>QueryPlanDefaults.GetParameterQueryResourceTypes()</c>.
    /// </summary>
    public HashSet<string> ParameterQueryResourceTypes { get; set; } = [];

    /// <summary>
    /// The set of FHIR resource types that the selected measures' CQL actually retrieves
    /// (e.g. <c>[Encounter]</c>, <c>[Observation]</c>, <c>[Device]</c>).
    /// Extracted from the measure bundle Library resources by <see cref="CqlResourceTypeExtractor"/>.
    /// Only resources of these types will be contained in the MeasureReport and therefore
    /// appear in ABS patient artifacts.
    /// When empty, CQL filtering is disabled (backward-compatible).
    /// </summary>
    public HashSet<string> CqlReferencedResourceTypes { get; set; } = [];

    /// <summary>
    /// Optional deterministic simulation of Data Acquisition output at resource-key level
    /// (Type/Id) by patient, computed from generated bundles + query plan semantics.
    /// When populated, this is used as the acquired set for ABS/ReportResource expectations.
    /// </summary>
    public IReadOnlyDictionary<string, HashSet<string>> SimulatedAcquiredResourceKeysByPatient { get; set; }
        = new Dictionary<string, HashSet<string>>();

    /// <summary>
    /// Every resource key (<c>ResourceType/ResourceId</c>) from the generated FHIR bundles,
    /// keyed per patient. Shared infrastructure resources are stored under the empty-string key.
    /// </summary>
    public IReadOnlyDictionary<string, HashSet<string>> ResourceKeysByPatient { get; init; }
        = new Dictionary<string, HashSet<string>>();

    /// <summary>
    /// Per-patient resource type -> count map, derived from the generated bundles.
    /// Key = patient ID, Value = { ResourceType -> count }.
    /// Shared infrastructure resources are stored under the empty-string key.
    /// </summary>
    public IReadOnlyDictionary<string, Dictionary<string, int>> ResourceCountsByPatientType { get; init; }
        = new Dictionary<string, Dictionary<string, int>>();

    /// <summary>Aggregate resource type -> count across all patients (excluding shared).</summary>
    public IReadOnlyDictionary<string, int> TotalCountsByType { get; init; }
        = new Dictionary<string, int>();

    /// <summary>Total number of generated resource entries across all bundles.</summary>
    public int TotalResourceCount { get; init; }

    // ----- Acquired / Expected-in-ABS resource type filters -----

    /// <summary>
    /// Returns true if the given resource type is one that the query plan acquires
    /// (Parameter or Reference query). Returns true for all types when
    /// <see cref="AcquiredResourceTypes"/> has not been configured (backward-compatible).
    /// Always returns true for "Patient" (anchor resource).
    /// Always returns false for pipeline-derived types (MeasureReport, OperationOutcome).
    /// </summary>
    public bool IsAcquiredType(string resourceType)
    {
        if (PipelineDerivedTypes.Contains(resourceType))
            return false;
        if (string.Equals(resourceType, "Patient", StringComparison.OrdinalIgnoreCase))
            return true;
        if (AcquiredResourceTypes.Count == 0)
            return true; // no filter configured - allow all
        return AcquiredResourceTypes.Contains(resourceType);
    }

    /// <summary>
    /// Returns true if the given resource type is expected in the final ABS patient
    /// artifacts (and therefore in the ReportResource DB, which stores the same data).
    ///
    /// A type is expected when it satisfies <b>all three</b> conditions:
    /// <list type="number">
    ///   <item>We <b>generated</b> it (caller checks from manifest data).</item>
    ///   <item>The query plan <b>acquires</b> it (<see cref="AcquiredResourceTypes"/>).</item>
    ///   <item>The measures' CQL <b>references</b> it (<see cref="CqlReferencedResourceTypes"/>).</item>
    /// </list>
    ///
    /// <b>How the pipeline works:</b> Data Acquisition runs the query plan (Parameter +
    /// Reference queries) and sends every acquired resource to MeasureEval. MeasureEval
    /// bundles them all and evaluates the CQL. The CQL engine loads every resource whose
    /// type appears in a <c>[ResourceType]</c> retrieve expression into the MeasureReport's
    /// contained list. PatientAggregator extracts those contained resources and writes them
    /// to the patient NDJSON in ABS and to the ReportResource DB.
    ///
    /// Both Parameter-query types (Encounter, Condition, Observation, etc.) and Reference-query
    /// types (Location, Medication, Device, Specimen) follow the same path: generated ->
    /// acquired -> bundled -> CQL-evaluated -> contained -> ABS. No tolerance or special-casing
    /// is needed - the system is deterministic given the input data.
    /// </summary>
    public bool IsExpectedInAbs(string resourceType)
    {
        if (PipelineDerivedTypes.Contains(resourceType))
            return false;
        if (string.Equals(resourceType, "Patient", StringComparison.OrdinalIgnoreCase))
            return true;

        if (!IsAcquiredType(resourceType))
            return false;

        if (CqlReferencedResourceTypes.Count > 0)
            return CqlReferencedResourceTypes.Contains(resourceType);

        // Fallback when CQL analysis is unavailable - use Parameter-query heuristic
        if (ParameterQueryResourceTypes.Count > 0)
            return ParameterQueryResourceTypes.Contains(resourceType);

        return true; // no filters configured - allow all acquired
    }

    /// <summary>
    /// Returns the per-patient resource type -> count map filtered to only types
    /// expected in ABS (generated -> acquired -> CQL-referenced).
    /// </summary>
    public Dictionary<string, int>? GetExpectedAbsCountsForPatient(string patientId)
    {
        var expectedKeys = GetExpectedAbsKeysForPatient(patientId);
        if (expectedKeys.Count == 0)
            return new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        return expectedKeys
            .Select(GetResourceTypeFromKey)
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .GroupBy(t => t, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Returns expected ABS resource keys for a patient using deterministic key-level logic:
    /// simulated-acquired (when available) + reachable CQL types.
    /// Falls back to generated keys when acquisition simulation is unavailable.
    /// </summary>
    public HashSet<string> GetExpectedAbsKeysForPatient(string patientId)
    {
        HashSet<string>? sourceKeys = null;

        if (SimulatedAcquiredResourceKeysByPatient.TryGetValue(patientId, out var simulated) && simulated.Count > 0)
            sourceKeys = simulated;
        else if (ResourceKeysByPatient.TryGetValue(patientId, out var generated) && generated.Count > 0)
            sourceKeys = generated;

        if (sourceKeys == null)
            return [];

        var filtered = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var key in sourceKeys)
        {
            var resourceType = GetResourceTypeFromKey(key);
            if (IsExpectedInAbs(resourceType))
                filtered.Add(key);
        }

        return filtered;
    }

    /// <summary>
    /// Returns all generated resource keys (Type/Id) for patient-scoped resources
    /// (excludes shared infrastructure under the empty-string key), filtered to only
    /// types expected in ABS (acquired + CQL-referenced).
    /// </summary>
    public HashSet<string> AllExpectedAbsPatientResourceKeys()
    {
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var patientId in PatientIds)
        {
            foreach (var key in GetExpectedAbsKeysForPatient(patientId))
                keys.Add(key);
        }
        return keys;
    }

    // ----- Derived convenience methods -----

    /// <summary>
    /// Returns the number of patients that qualify for the given pipeline measure ID.
    /// </summary>
    public int QualifyingPatientCount(string measureId)
    {
        var idx = IndexOfMeasure(measureId);
        if (idx < 0 || idx >= SelectedMeasures.Count) return Profiles.Count; // fallback: assume all qualify
        var measureType = SelectedMeasures[idx];
        return Profiles.Count(p => p.QualifiesFor(measureType));
    }

    private static string GetResourceTypeFromKey(string key)
    {
        var slashIdx = key.IndexOf('/');
        return slashIdx > 0 ? key[..slashIdx] : key;
    }

    /// <summary>
    /// Builds the <c>measureId -> qualifying patient count</c> map used by
    /// <see cref="LantanaGroup.Link.Automation.Link.Validation.ReportDatabaseValidator"/>.
    /// </summary>
    public Dictionary<string, int> BuildQualifyingCountPerMeasure()
        => PatientProfile.BuildQualifyingCountPerMeasure(Profiles, SelectedMeasures, MeasureIds);

    /// <summary>
    /// Returns which patient IDs should be submitted (qualify for at least one measure).
    /// </summary>
    public List<string> ExpectedSubmittedPatientIds()
    {
        var result = new List<string>();
        for (var i = 0; i < PatientIds.Count && i < Profiles.Count; i++)
        {
            if (Profiles[i].QualifiesForAny(SelectedMeasures))
                result.Add(PatientIds[i]);
        }
        return result;
    }

    /// <summary>
    /// Returns all generated resource keys (Type/Id) across all patients, excluding shared
    /// infrastructure entries. Includes ALL generated types (not filtered by query plan).
    /// For ABS-expected keys, use <see cref="AllExpectedAbsPatientResourceKeys"/>.
    /// </summary>
    public HashSet<string> AllPatientResourceKeys()
    {
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (patientId, patientKeys) in ResourceKeysByPatient)
        {
            if (string.IsNullOrEmpty(patientId)) continue; // skip shared
            foreach (var k in patientKeys) keys.Add(k);
        }
        return keys;
    }

    private int IndexOfMeasure(string measureId)
    {
        for (var i = 0; i < MeasureIds.Count; i++)
        {
            if (string.Equals(MeasureIds[i], measureId, StringComparison.OrdinalIgnoreCase))
                return i;
        }
        return -1;
    }

    // ----- Factory -----

    /// <summary>
    /// Builds a manifest from the generated FHIR bundles and profile data.
    /// Call once after <see cref="FhirBundleGenerator"/> returns.
    /// </summary>
    public static GenerationManifest Build(
        IReadOnlyList<string> patientIds,
        IReadOnlyList<(string Name, string Json)> bundles,
        IReadOnlyList<PatientProfile> profiles,
        IReadOnlyList<ProfiledMeasureType> selectedMeasures)
    {
        var keysByPatient = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        var countsByPatientType = new Dictionary<string, Dictionary<string, int>>(StringComparer.Ordinal);
        var totalsByType = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var patientIdSet = patientIds.ToHashSet(StringComparer.Ordinal);
        var totalCount = 0;

        foreach (var (_, json) in bundles)
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("entry", out var entries) || entries.ValueKind != JsonValueKind.Array)
                continue;

            foreach (var entry in entries.EnumerateArray())
            {
                if (!entry.TryGetProperty("request", out var request) || request.ValueKind != JsonValueKind.Object)
                    continue;

                var url = request.TryGetProperty("url", out var urlProp) && urlProp.ValueKind == JsonValueKind.String
                    ? urlProp.GetString()
                    : null;

                if (string.IsNullOrWhiteSpace(url) || !url.Contains('/'))
                    continue;

                totalCount++;
                var slashIdx = url.IndexOf('/');
                var resourceType = url[..slashIdx];
                var resourceId = url[(slashIdx + 1)..];

                // Determine which patient this resource belongs to.
                var ownerPatientId = string.Empty; // shared by default
                foreach (var pid in patientIdSet)
                {
                    if (resourceId.StartsWith(pid, StringComparison.Ordinal))
                    {
                        ownerPatientId = pid;
                        break;
                    }
                }

                if (!keysByPatient.TryGetValue(ownerPatientId, out var keys))
                {
                    keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    keysByPatient[ownerPatientId] = keys;
                }
                keys.Add(url);

                if (!countsByPatientType.TryGetValue(ownerPatientId, out var typeCounts))
                {
                    typeCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                    countsByPatientType[ownerPatientId] = typeCounts;
                }
                typeCounts[resourceType] = typeCounts.TryGetValue(resourceType, out var c) ? c + 1 : 1;

                // Aggregate totals (patient resources only, not shared)
                if (!string.IsNullOrEmpty(ownerPatientId))
                    totalsByType[resourceType] = totalsByType.TryGetValue(resourceType, out var tc) ? tc + 1 : 1;
            }
        }

        return new GenerationManifest
        {
            PatientIds = patientIds,
            Profiles = profiles,
            SelectedMeasures = selectedMeasures,
            ResourceKeysByPatient = keysByPatient,
            ResourceCountsByPatientType = countsByPatientType,
            TotalCountsByType = totalsByType,
            TotalResourceCount = totalCount
        };
    }
}
