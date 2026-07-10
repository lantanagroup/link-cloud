using Hl7.Fhir.Model;
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
    /// Resource types that are pipeline-derived (not produced by our FHIR generator).
    /// They appear in ABS as a deterministic function of the pipeline, not of generated input:
    /// <list type="bullet">
    ///   <item><c>MeasureReport</c> — MeasureEval writes one per submitted patient per measure.</item>
    ///   <item><c>OperationOutcome</c> — one per patient that fails validation. Normally 0;
    ///         callers set <see cref="ExpectedOperationOutcomeCountByPatient"/> when failures are expected.</item>
    /// </list>
    /// These types are never compared key-for-key; instead a count-level prediction is added
    /// (<see cref="GetExpectedAbsCountsForPatient"/>) so strict prediction-vs-actual reconciliation works.
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
    /// Resource keys (<c>Type/Id</c>) that were generated but are expected to be filtered
    /// out by CQL SDE <c>where</c> clauses at the individual-resource level.
    ///
    /// Unlike <see cref="CqlReferencedResourceTypes"/> (which filters whole types),
    /// this captures <b>per-resource</b> exclusions: e.g. a Condition that is "resolved"
    /// when the CQL requires "active", or a Condition whose recordedDate falls outside
    /// the encounter period.
    ///
    /// Keyed by patient ID. The prediction subtracts these keys from the expected set.
    /// Built at generation time by replaying the known CQL filter criteria against each
    /// generated resource's attributes.
    /// </summary>
    public IReadOnlyDictionary<string, HashSet<string>> CqlFilteredResourceKeysByPatient { get; set; }
        = new Dictionary<string, HashSet<string>>();

    /// <summary>
    /// Per-patient count of expected <c>OperationOutcome</c> resources in ABS.
    /// Defaults to 0 (strict: no validation failures expected).
    ///
    /// The Validation service emits one OperationOutcome per patient when validation fails.
    /// Tests or post-run validators that know which patients failed can populate this map
    /// to keep the strict prediction-vs-actual comparison passing.
    /// </summary>
    public IDictionary<string, int> ExpectedOperationOutcomeCountByPatient { get; set; }
        = new Dictionary<string, int>(StringComparer.Ordinal);

    /// <summary>
    /// When true, predict one additional <c>Organization</c> resource per submitted patient
    /// artifact in ABS to account for Report's <c>PatientAggregator:IncludeOrganizationResource</c>
    /// behavior.
    ///
    /// This is count-level only: the organization ID is assigned by downstream services and
    /// is not represented in key-level expectations.
    /// </summary>
    public bool IncludePatientAggregatorOrganizationResource { get; set; }

    /// <summary>
    /// Optional explicit ABS scope: when populated, only these patients are expected
    /// to produce patient-{id}.ndjson artifacts.
    ///
    /// This is used by scheduled workflows where a patient's profile may be
    /// measure-qualifying in isolation, but report orchestration/terminal state resolves
    /// a smaller submitted subset.
    /// </summary>
    public IReadOnlySet<string>? ExpectedAbsPatientIdsOverride { get; set; }

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

    /// <summary>
    /// Patient IDs whose data was already on the FHIR server before the run (imported by ID).
    /// These patients are included in the manifest and report, but their resources are NOT
    /// tracked for cleanup expunge — only resources we uploaded during the run are tracked.
    /// </summary>
    public IReadOnlySet<string> PreExistingPatientIds { get; init; }
        = new HashSet<string>(StringComparer.Ordinal);

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
    /// <b>How the pipeline works:</b>
    /// <list type="number">
    ///   <item>Data Acquisition runs the query plan and sends every acquired resource to MeasureEval.</item>
    ///   <item>MeasureEval bundles all acquired resources as <c>additionalData</c> and evaluates the CQL.</item>
    ///   <item>The HAPI CQL engine places resources into <c>MeasureReport.contained</c> only when they
    ///         are touched by a CQL <c>[ResourceType]</c> retrieve expression. Resources in
    ///         <c>additionalData</c> whose type has no matching retrieve are <b>not</b> included
    ///         in <c>contained</c>.</item>
    ///   <item>MeasureEval's <c>normalize()</c> extracts <c>contained</c> resources and writes them
    ///         (plus the MeasureReport itself) to the <c>.mr</c> file in ABS.</item>
    ///   <item>PatientAggregator reads the <c>.mr</c> files and writes every resource from them
    ///         to <c>patient-{id}.ndjson</c> in ABS and to the ReportResource DB.</item>
    /// </list>
    ///
    /// Therefore: Generated ? QP-Acquired ? CQL-Referenced = Expected in ABS.
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

        // Fallback when CQL analysis is unavailable — assume all acquired types pass
        return true;
    }

    /// <summary>
    /// Returns the per-patient resource type -> count map filtered to only types
    /// expected in the ABS patient NDJSON artifact (generated ? query-plan-acquired ?
    /// CQL-referenced), plus deterministic pipeline-derived additions:
    /// <list type="bullet">
    ///   <item><c>Patient</c> — always 1 for patients that qualify for any selected measure
    ///         (MeasureEval's CQL engine loads Patient implicitly, so it always lands in ABS).</item>
    ///   <item><c>MeasureReport</c> — one per measure the patient qualifies for
    ///         (MeasureEval writes exactly one MeasureReport per submitted patient per measure).</item>
    ///   <item><c>OperationOutcome</c> — the caller-supplied expectation from
    ///         <see cref="ExpectedOperationOutcomeCountByPatient"/> (default 0). Appended
    ///         directly to the patient aggregate blob by <c>ValidationCompleteListener</c>
    ///         for any patient whose <c>ValidationComplete.IsValid == false</c>.</item>
    /// </list>
    /// </summary>
    public Dictionary<string, int>? GetExpectedAbsCountsForPatient(string patientId)
        => BuildExpectedCountsForPatient(patientId);

    private Dictionary<string, int>? BuildExpectedCountsForPatient(string patientId)
    {
        var expectedKeys = GetExpectedAbsKeysForPatient(patientId);
        var counts = expectedKeys
            .Select(GetResourceTypeFromKey)
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .GroupBy(t => t, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);

        // Pipeline-derived additions — count-level only because IDs are assigned downstream.
        AddPipelineDerivedExpectedCounts(patientId, counts);

        return counts;
    }

    /// <summary>
    /// Returns expected ABS resource keys for a patient using deterministic key-level logic:
    /// simulated-acquired keys (when available) filtered to types that pass all three gates
    /// (generated ? acquired ? CQL-referenced). Falls back to generated keys when
    /// acquisition simulation is unavailable.
    ///
    /// For patients qualifying for any selected measure, <c>Patient/{patientId}</c> is always
    /// included — MeasureEval's CQL engine loads the Patient resource as an implicit anchor
    /// even if the query plan has no explicit Patient query.
    ///
    /// Pipeline-derived resources (<c>MeasureReport</c>, <c>OperationOutcome</c>) are
    /// intentionally <b>not</b> included here because their IDs are assigned downstream.
    /// They contribute count-level expectations via <see cref="GetExpectedAbsCountsForPatient"/>.
    /// </summary>
    public HashSet<string> GetExpectedAbsKeysForPatient(string patientId)
    {
        var filtered = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (ExpectedAbsPatientIdsOverride is { Count: > 0 }
            && !ExpectedAbsPatientIdsOverride.Contains(patientId))
        {
            return filtered;
        }

        // Non-qualifying patients are never submitted by measure-eval, so PatientAggregator
        // never emits a patient-{id}.ndjson artifact for them and nothing about them
        // appears in ABS — even though their resources were uploaded to the FHIR server
        // and acquired by Data Acquisition. Returning an empty expectation here is what
        // keeps mixed Q + NQ manifests (e.g. NQ cohorts combined with imported bundles)
        // from generating spurious "missing expected resource" errors against ABS.
        if (!QualifiesForAnySelectedMeasure(patientId))
            return filtered;

        HashSet<string>? sourceKeys = null;

        if (SimulatedAcquiredResourceKeysByPatient.TryGetValue(patientId, out var simulated) && simulated.Count > 0)
            sourceKeys = simulated;
        else if (ResourceKeysByPatient.TryGetValue(patientId, out var generated) && generated.Count > 0)
            sourceKeys = generated;

        if (sourceKeys != null)
        {
            // Per-resource CQL filter exclusions (e.g. resolved Conditions, out-of-period recordedDate)
            CqlFilteredResourceKeysByPatient.TryGetValue(patientId, out var cqlFiltered);

            foreach (var key in sourceKeys)
            {
                var resourceType = GetResourceTypeFromKey(key);
                if (!IsExpectedInAbs(resourceType))
                    continue;
                if (cqlFiltered != null && cqlFiltered.Contains(key))
                    continue;
                filtered.Add(key);
            }
        }

        // MeasureEval's CQL engine always loads Patient as the context anchor, regardless
        // of whether the query plan has an explicit Patient query.
        filtered.Add($"Patient/{patientId}");

        return filtered;
    }

    /// <summary>
    /// Adds count-level predictions for pipeline-derived resource types that have no
    /// deterministic key-level prediction (IDs assigned downstream).
    /// <c>OperationOutcome</c> is appended directly to the patient aggregate blob by
    /// <c>ValidationCompleteListener</c> when <c>ValidationComplete.IsValid == false</c>.
    /// </summary>
    private void AddPipelineDerivedExpectedCounts(string patientId, Dictionary<string, int> counts)
    {
        if (!ShouldExpectAbsForPatient(patientId))
            return;

        var qualifyingMeasureCount = CountQualifyingMeasuresForPatient(patientId);
        if (qualifyingMeasureCount > 0)
        {
            // MeasureEval produces exactly one MeasureReport per patient per qualifying measure.
            counts["MeasureReport"] = qualifyingMeasureCount;
        }

        if (ExpectedOperationOutcomeCountByPatient != null
            && ExpectedOperationOutcomeCountByPatient.TryGetValue(patientId, out var ooCount)
            && ooCount > 0)
        {
            counts["OperationOutcome"] = ooCount;
        }

        if (IncludePatientAggregatorOrganizationResource)
        {
            counts["Organization"] = counts.TryGetValue("Organization", out var orgCount)
                ? orgCount + 1
                : 1;
        }
    }

    private int CountQualifyingMeasuresForPatient(string patientId)
    {
        var idx = PatientIds.ToList().IndexOf(patientId);
        if (idx < 0 || idx >= Profiles.Count) return 0;
        var profile = Profiles[idx];
        if (!profile.IsExpectedInReportByCohortAndPattern())
            return 0;
        return SelectedMeasures.Count(m => profile.QualifiesFor(m));
    }

    private bool QualifiesForAnySelectedMeasure(string patientId)
    {
        var idx = PatientIds.ToList().IndexOf(patientId);
        if (idx < 0 || idx >= Profiles.Count) return false;
        return Profiles[idx].IsExpectedToBeSubmitted(SelectedMeasures);
    }

    private bool ShouldExpectAbsForPatient(string patientId)
    {
        return ExpectedAbsPatientIdsOverride is not { Count: > 0 }
               || ExpectedAbsPatientIdsOverride.Contains(patientId);
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
        if (idx < 0 || idx >= SelectedMeasures.Count)
            return Profiles.Count(p => p.IsExpectedInReportByCohortAndPattern());
        var measureType = SelectedMeasures[idx];
        return Profiles.Count(p => p.IsExpectedInReportByCohortAndPattern() && p.QualifiesFor(measureType));
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
            if (Profiles[i].IsExpectedToBeSubmitted(SelectedMeasures))
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

    // ----- Snapshot for UI persistence -----

    /// <summary>
    /// Creates a lightweight, serializable snapshot suitable for storage and display.
    /// </summary>
    public GenerationManifestSnapshot ToSnapshot()
    {
        var eligibility = new Dictionary<string, List<string>>();
        var inpatientPatterns = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var i = 0; i < PatientIds.Count && i < Profiles.Count; i++)
        {
            var qualifying = new List<string>();
            foreach (var measure in SelectedMeasures)
            {
                if (Profiles[i].QualifiesFor(measure))
                    qualifying.Add(measure.ToString());
            }
            eligibility[PatientIds[i]] = qualifying;

            var pattern = Profiles[i].ScheduledInpatientPattern
                ?? ScheduledInpatientPattern.AdmittedBeforePeriodRemainsInpatientAfterPeriod;
            inpatientPatterns[PatientIds[i]] = pattern.ToString();
        }

        // Build predicted ABS counts per patient (generated ? acquired ? CQL-referenced)
        var expectedAbsByPatient = new Dictionary<string, Dictionary<string, int>>(StringComparer.Ordinal);
        var expectedAbsTotals = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var patientId in PatientIds)
        {
            var counts = GetExpectedAbsCountsForPatient(patientId);
            if (counts != null && counts.Count > 0)
            {
                expectedAbsByPatient[patientId] = counts;
                foreach (var (type, count) in counts)
                    expectedAbsTotals[type] = expectedAbsTotals.TryGetValue(type, out var c) ? c + count : count;
            }
        }

        return new GenerationManifestSnapshot
        {
            PatientCount = PatientIds.Count,
            TotalResourceCount = TotalResourceCount,
            PatientIds = PatientIds,
            SelectedMeasures = SelectedMeasures.Select(m => m.ToString()).ToList(),
            MeasureIds = MeasureIds,
            AcquiredResourceTypes = AcquiredResourceTypes.OrderBy(t => t, StringComparer.OrdinalIgnoreCase).ToList(),
            ParameterQueryResourceTypes = ParameterQueryResourceTypes.OrderBy(t => t, StringComparer.OrdinalIgnoreCase).ToList(),
            CqlReferencedResourceTypes = CqlReferencedResourceTypes.OrderBy(t => t, StringComparer.OrdinalIgnoreCase).ToList(),
            TotalCountsByType = TotalCountsByType.ToDictionary(kv => kv.Key, kv => kv.Value),
            SharedInfrastructureCountsByType = ResourceCountsByPatientType
                .Where(kv => string.IsNullOrEmpty(kv.Key))
                .SelectMany(kv => kv.Value)
                .ToDictionary(kv => kv.Key, kv => kv.Value),
            ResourceCountsByPatient = ResourceCountsByPatientType
                .Where(kv => !string.IsNullOrEmpty(kv.Key))
                .ToDictionary(kv => kv.Key, kv => new Dictionary<string, int>(kv.Value)),
            PatientEligibility = eligibility,
            PatientInpatientPatterns = inpatientPatterns,
            ExpectedAbsCountsByPatient = expectedAbsByPatient,
            ExpectedAbsTotalCountsByType = expectedAbsTotals
        };
    }

    // ----- Incremental builder (pipeline-friendly) -----

    /// <summary>
    /// Accumulates manifest metadata incrementally from in-memory FHIR bundle entries
    /// so the pipeline can build a manifest without retaining serialized JSON bundles.
    /// Thread-safe for concurrent patient processing.
    /// </summary>
    public sealed class IncrementalBuilder
    {
        private readonly object _lock = new();
        private readonly List<string> _patientIds = [];
        private readonly List<PatientProfile> _profiles = [];
        private readonly Dictionary<string, HashSet<string>> _keysByPatient = new(StringComparer.Ordinal);
        private readonly Dictionary<string, Dictionary<string, int>> _countsByPatientType = new(StringComparer.Ordinal);
        private readonly Dictionary<string, int> _totalsByType = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, HashSet<string>> _simulatedAcquiredKeys = new(StringComparer.Ordinal);
        private readonly HashSet<string> _preExistingPatientIds = new(StringComparer.Ordinal);
        private int _totalCount;

        /// <summary>
        /// Records a batch of FHIR bundle entries for a single patient (or shared infrastructure).
        /// Call once per patient with all entries for that patient.
        /// <paramref name="ownerPatientId"/> should be empty-string for shared infrastructure entries.
        /// </summary>
        public void AddEntries(string ownerPatientId, IReadOnlyList<Bundle.EntryComponent> entries)
        {
            var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var typeCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            foreach (var entry in entries)
            {
                var url = entry.Request?.Url;
                if (string.IsNullOrWhiteSpace(url) || !url.Contains('/'))
                    continue;

                var slashIdx = url.IndexOf('/');
                var resourceType = url[..slashIdx];

                keys.Add(url);
                typeCounts[resourceType] = typeCounts.TryGetValue(resourceType, out var c) ? c + 1 : 1;
            }

            lock (_lock)
            {
                _totalCount += keys.Count;

                if (!_keysByPatient.TryGetValue(ownerPatientId, out var existing))
                {
                    _keysByPatient[ownerPatientId] = keys;
                }
                else
                {
                    foreach (var k in keys) existing.Add(k);
                }

                if (!_countsByPatientType.TryGetValue(ownerPatientId, out var existingCounts))
                {
                    _countsByPatientType[ownerPatientId] = typeCounts;
                }
                else
                {
                    foreach (var (t, cnt) in typeCounts)
                        existingCounts[t] = existingCounts.TryGetValue(t, out var ec) ? ec + cnt : cnt;
                }

                if (!string.IsNullOrEmpty(ownerPatientId))
                {
                    foreach (var (t, cnt) in typeCounts)
                        _totalsByType[t] = _totalsByType.TryGetValue(t, out var tc) ? tc + cnt : cnt;
                }
            }
        }

        /// <summary>
        /// Records a patient ID and its profile. Must be called in order of patient generation.
        /// </summary>
        public void AddPatient(string patientId, PatientProfile profile)
        {
            lock (_lock)
            {
                _patientIds.Add(patientId);
                _profiles.Add(profile);
            }
        }

        /// <summary>
        /// Records simulated acquisition results for a patient.
        /// </summary>
        public void SetSimulatedAcquiredKeys(string patientId, HashSet<string> acquiredKeys)
        {
            lock (_lock)
            {
                _simulatedAcquiredKeys[patientId] = acquiredKeys;
            }
        }

        private readonly Dictionary<string, HashSet<string>> _cqlFilteredKeys = new(StringComparer.Ordinal);

        /// <summary>
        /// Records resource keys that will be filtered out by CQL SDE where-clauses
        /// at the individual-resource level for the given patient.
        /// </summary>
        public void SetCqlFilteredKeys(string patientId, HashSet<string> filteredKeys)
        {
            if (filteredKeys.Count == 0) return;
            lock (_lock)
            {
                _cqlFilteredKeys[patientId] = filteredKeys;
            }
        }

        /// <summary>
        /// Marks the given patient as one whose data already existed on the FHIR server
        /// (imported by ID rather than generated or uploaded). The cleanup step uses this
        /// to skip expunging the patient's resources.
        /// </summary>
        public void MarkPreExistingPatient(string patientId)
        {
            if (string.IsNullOrEmpty(patientId)) return;
            lock (_lock)
            {
                _preExistingPatientIds.Add(patientId);
            }
        }

        /// <summary>
        /// Builds the final <see cref="GenerationManifest"/> from all accumulated data.
        /// Call once after all patients have been processed.
        /// </summary>
        public GenerationManifest Build(IReadOnlyList<ProfiledMeasureType> selectedMeasures)
        {
            lock (_lock)
            {
                return new GenerationManifest
                {
                    PatientIds = _patientIds.ToList(),
                    Profiles = _profiles.ToList(),
                    SelectedMeasures = selectedMeasures,
                    ResourceKeysByPatient = new Dictionary<string, HashSet<string>>(_keysByPatient, StringComparer.Ordinal),
                    ResourceCountsByPatientType = new Dictionary<string, Dictionary<string, int>>(_countsByPatientType, StringComparer.Ordinal),
                    TotalCountsByType = new Dictionary<string, int>(_totalsByType, StringComparer.OrdinalIgnoreCase),
                    TotalResourceCount = _totalCount,
                    SimulatedAcquiredResourceKeysByPatient = new Dictionary<string, HashSet<string>>(_simulatedAcquiredKeys, StringComparer.Ordinal),
                    CqlFilteredResourceKeysByPatient = new Dictionary<string, HashSet<string>>(_cqlFilteredKeys, StringComparer.Ordinal),
                    PreExistingPatientIds = new HashSet<string>(_preExistingPatientIds, StringComparer.Ordinal)
                };
            }
        }
    }
}
