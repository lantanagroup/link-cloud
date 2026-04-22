namespace LantanaGroup.Automation.Generation;

/// <summary>
/// Simulates measure-specific CQL SDE <c>where</c>-clause filtering at the individual-resource level.
///
/// Type-level reachability (<c>[ResourceType]</c>) is handled elsewhere. This simulator focuses on
/// per-resource exclusions where CQL retrieves a type but includes only rows matching additional
/// predicates (status/date/category/reference constraints).
///
/// Operates on <b>actual extracted resource attributes</b> (via <see cref="CqlFilterInputExtractor"/>)
/// rather than seed replay, so it stays accurate even if generator internals drift.
/// </summary>
public static class CqlFilterSimulator
{
    private static readonly IReadOnlyList<ICqlFilterProfile> Profiles =
    [
        new AchConditionFilterProfile(),
        new HypoglycemicConditionFilterProfile(),
        new AchObservationFilterProfile(),
        new HypoglycemicObservationFilterProfile()
    ];

    /// <summary>
    /// Computes resource keys CQL SDE <c>where</c> clauses will exclude for the patient.
    ///
    /// The intersection rule is applied <b>per resource type</b>: a key is excluded only when
    /// every applicable profile that targets that resource type excludes it. Profiles for
    /// other resource types do not participate in that intersection — an Observation profile
    /// has no opinion about whether a Condition belongs in ABS, and vice-versa.
    ///
    /// MeasureEval evaluates each measure independently and writes one <c>.mr</c> file per
    /// measure; PatientAggregator unions the contained resources across those files when
    /// producing the patient NDJSON. So if any one applicable measure includes the resource,
    /// it appears in ABS regardless of the others.
    /// </summary>
    public static HashSet<string> ComputeFilteredKeys(
        IReadOnlyList<ProfiledMeasureType> measures,
        PatientCqlInput input)
    {
        if (measures == null || measures.Count == 0 || input == null)
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var perTypeExclusions = new Dictionary<string, List<HashSet<string>>>(StringComparer.OrdinalIgnoreCase);
        foreach (var profile in Profiles)
        {
            if (!profile.AppliesToAny(measures))
                continue;

            if (!perTypeExclusions.TryGetValue(profile.TargetResourceType, out var bucket))
            {
                bucket = new List<HashSet<string>>();
                perTypeExclusions[profile.TargetResourceType] = bucket;
            }
            bucket.Add(profile.ComputeExcludedKeys(input));
        }

        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var bucket in perTypeExclusions.Values)
        {
            // Intersect within a resource type: keep keys every applicable profile of that
            // type excludes. Then union across types into the final excluded-key set.
            var intersection = new HashSet<string>(bucket[0], StringComparer.OrdinalIgnoreCase);
            for (var i = 1; i < bucket.Count; i++)
                intersection.IntersectWith(bucket[i]);

            foreach (var key in intersection)
                result.Add(key);
        }

        return result;
    }

    /// <summary>
    /// Extracted per-patient inputs used by the simulator.
    /// Build via <see cref="CqlFilterInputExtractor"/>.
    /// </summary>
    public sealed record PatientCqlInput(
        string PatientId,
        string EncounterId,
        DateTime EncounterStart,
        DateTime EncounterEnd,
        IReadOnlyList<ConditionContext> Conditions,
        IReadOnlyList<ObservationContext> Observations);

    public interface ICqlFilterProfile
    {
        /// <summary>
        /// FHIR resource type this profile produces exclusions for (e.g. <c>Condition</c>,
        /// <c>Observation</c>). Used by <see cref="ComputeFilteredKeys"/> to scope the
        /// intersection rule to profiles that operate on the same resource type.
        /// </summary>
        string TargetResourceType { get; }

        bool AppliesToAny(IReadOnlyList<ProfiledMeasureType> measures);
        HashSet<string> ComputeExcludedKeys(PatientCqlInput input);
    }

    private abstract class ConditionFilterProfileBase : ICqlFilterProfile
    {
        public string TargetResourceType => "Condition";

        public abstract bool AppliesToAny(IReadOnlyList<ProfiledMeasureType> measures);

        protected abstract bool IncludeCondition(
            ConditionContext c,
            DateTime encounterEnd,
            string encounterId);

        public HashSet<string> ComputeExcludedKeys(PatientCqlInput input)
        {
            var excluded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var c in input.Conditions)
            {
                if (!IncludeCondition(c, input.EncounterEnd, input.EncounterId))
                    excluded.Add($"Condition/{c.ResourceId}");
            }
            return excluded;
        }
    }

    /// <summary>
    /// ACH Monthly + ACH Daily SDE Condition semantics:
    /// - problem-list-item requires active + recordedDate strictly before encounter end date
    /// - OR encounter-diagnosis/health-concern tied to the encounter
    /// </summary>
    private sealed class AchConditionFilterProfile : ConditionFilterProfileBase
    {
        public override bool AppliesToAny(IReadOnlyList<ProfiledMeasureType> measures) =>
            measures.Contains(ProfiledMeasureType.NhsnAcuteCareHospitalMonthlyInitialPopulation)
            || measures.Contains(ProfiledMeasureType.NhsnAcuteCareHospitalDailyInitialPopulation);

        protected override bool IncludeCondition(ConditionContext c, DateTime encounterEnd, string encounterId)
        {
            if (c.HasCategory("problem-list-item") && c.IsActive && c.RecordedDate < encounterEnd.Date)
                return true;

            if ((c.HasCategory("encounter-diagnosis") || c.HasCategory("health-concern"))
                && EncounterMatches(c.EncounterReference, encounterId))
            {
                return true;
            }

            return false;
        }
    }

    /// <summary>
    /// Hypoglycemic SDE Condition semantics:
    /// - conditions overlapping Initial Population period are included
    /// - no active-status constraint in this measure's SDE Condition define.
    /// </summary>
    private sealed class HypoglycemicConditionFilterProfile : ConditionFilterProfileBase
    {
        public override bool AppliesToAny(IReadOnlyList<ProfiledMeasureType> measures) =>
            measures.Contains(ProfiledMeasureType.NhsnGlycemicControlHypoglycemicInitialPopulation);

        protected override bool IncludeCondition(ConditionContext c, DateTime encounterEnd, string encounterId)
        {
            return c.RecordedDate <= encounterEnd.Date;
        }
    }

    /// <summary>
    /// CQL-relevant attributes of a generated Condition resource.
    /// Extracted from the actual generated FHIR content (no seed replay).
    /// </summary>
    public sealed record ConditionContext(
        string ResourceId,
        bool IsActive,
        DateTime RecordedDate,
        string EncounterReference,
        IReadOnlyList<string> CategoryCodes)
    {
        public bool HasCategory(string code) =>
            CategoryCodes.Any(c => string.Equals(c, code, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Compares a Condition.encounter reference (may be "Encounter/{id}" or just "{id}")
    /// against the patient's encounter id.
    /// </summary>
    private static bool EncounterMatches(string? encounterReference, string encounterId)
    {
        if (string.IsNullOrWhiteSpace(encounterReference))
            return false;

        var slash = encounterReference.IndexOf('/');
        var refId = slash >= 0 ? encounterReference[(slash + 1)..] : encounterReference;
        return string.Equals(refId, encounterId, StringComparison.OrdinalIgnoreCase);
    }

    // -------------------------------------------------------------------
    //  Observation profiles
    // -------------------------------------------------------------------

    /// <summary>
    /// CQL-relevant attributes of a generated Observation resource.
    /// Extracted from the actual generated FHIR content (no seed replay).
    /// <see cref="EffectiveStart"/> / <see cref="EffectiveEnd"/> normalize both
    /// <c>effectiveDateTime</c> (start == end) and <c>effectivePeriod</c> shapes.
    /// </summary>
    public sealed record ObservationContext(
        string ResourceId,
        string LoincCode,
        IReadOnlyList<string> CategoryCodes,
        DateTime EffectiveStart,
        DateTime EffectiveEnd)
    {
        public bool HasCategory(string code) =>
            CategoryCodes.Any(c => string.Equals(c, code, StringComparison.OrdinalIgnoreCase));

        /// <summary>
        /// True when the observation's effective range overlaps the closed interval
        /// [periodStart, periodEnd] using FHIR "overlaps" semantics.
        /// </summary>
        public bool OverlapsPeriod(DateTime periodStart, DateTime periodEnd) =>
            EffectiveStart <= periodEnd && EffectiveEnd >= periodStart;
    }

    private abstract class ObservationFilterProfileBase : ICqlFilterProfile
    {
        public string TargetResourceType => "Observation";

        public abstract bool AppliesToAny(IReadOnlyList<ProfiledMeasureType> measures);

        protected abstract bool IncludeObservation(
            ObservationContext o,
            DateTime encounterStart,
            DateTime encounterEnd);

        public HashSet<string> ComputeExcludedKeys(PatientCqlInput input)
        {
            var excluded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var o in input.Observations)
            {
                if (!IncludeObservation(o, input.EncounterStart, input.EncounterEnd))
                    excluded.Add($"Observation/{o.ResourceId}");
            }
            return excluded;
        }
    }

    /// <summary>
    /// ACH Monthly + ACH Daily SDE Observation semantics:
    /// <list type="bullet">
    ///   <item>SDE Observation Lab Category: <c>category ~ "laboratory"</c> AND effective overlaps IP.</item>
    ///   <item>SDE Observation Vital Signs Category: <c>category ~ "vital-signs"</c> AND effective overlaps IP.</item>
    ///   <item>SDE Observation Category (catch-all): <c>category</c> in
    ///         <c>{social-history, survey, imaging, procedure}</c> AND effective overlaps IP.</item>
    /// </list>
    /// IP for the simulator is approximated by the patient's encounter period, which is
    /// how the generator places observations and how the measures' Initial Population
    /// resolves for the synthetic patients (one qualifying encounter per patient).
    /// </summary>
    private sealed class AchObservationFilterProfile : ObservationFilterProfileBase
    {
        private static readonly HashSet<string> AchCategoryCodes = new(StringComparer.OrdinalIgnoreCase)
        {
            "laboratory",
            "vital-signs",
            "social-history",
            "survey",
            "imaging",
            "procedure"
        };

        public override bool AppliesToAny(IReadOnlyList<ProfiledMeasureType> measures) =>
            measures.Contains(ProfiledMeasureType.NhsnAcuteCareHospitalMonthlyInitialPopulation)
            || measures.Contains(ProfiledMeasureType.NhsnAcuteCareHospitalDailyInitialPopulation);

        protected override bool IncludeObservation(ObservationContext o, DateTime encounterStart, DateTime encounterEnd)
        {
            if (!o.CategoryCodes.Any(c => AchCategoryCodes.Contains(c)))
                return false;

            return o.OverlapsPeriod(encounterStart, encounterEnd);
        }
    }

    /// <summary>
    /// Hypoglycemic SDE Observation semantics:
    /// <c>[Observation: "Blood Glucose Laboratory and Point of Care Tests"]</c> retrieve,
    /// then <c>start of effective during InitialPopulation period</c>.
    ///
    /// Only blood-glucose lab/POC LOINCs are reachable; every other observation is dropped
    /// by the value-set bound retrieve regardless of category or effective date.
    /// The whitelist below is the subset of the measure's value set that the synthetic
    /// generator currently emits; expanding the generator pool is the only thing that
    /// would require expanding this whitelist.
    /// </summary>
    private sealed class HypoglycemicObservationFilterProfile : ObservationFilterProfileBase
    {
        private static readonly HashSet<string> BloodGlucoseLoincCodes = new(StringComparer.OrdinalIgnoreCase)
        {
            "2339-0",   // Glucose [Mass/volume] in Blood
            "2345-7",   // Glucose [Mass/volume] in Serum or Plasma
            "41653-7"   // Glucose [Mass/volume] in Capillary blood by Glucometer
        };

        public override bool AppliesToAny(IReadOnlyList<ProfiledMeasureType> measures) =>
            measures.Contains(ProfiledMeasureType.NhsnGlycemicControlHypoglycemicInitialPopulation);

        protected override bool IncludeObservation(ObservationContext o, DateTime encounterStart, DateTime encounterEnd)
        {
            if (!BloodGlucoseLoincCodes.Contains(o.LoincCode))
                return false;

            // Hypoglycemic uses "start of effective during IP" — point-in-IP semantics.
            return o.EffectiveStart >= encounterStart && o.EffectiveStart <= encounterEnd;
        }
    }
}
