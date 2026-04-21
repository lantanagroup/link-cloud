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
        new HypoglycemicConditionFilterProfile()
    ];

    /// <summary>
    /// Computes resource keys CQL SDE <c>where</c> clauses will exclude for the patient.
    ///
    /// The result is the <b>intersection</b> of exclusions across every profile applicable
    /// to the selected measures — a resource is only truly absent from ABS when every
    /// applicable measure excludes it. MeasureEval evaluates each measure independently and
    /// writes one <c>.mr</c> file per measure; PatientAggregator unions the contained
    /// resources across those files when producing the patient NDJSON. So if any one
    /// measure includes the resource, it appears in ABS regardless of the others.
    /// </summary>
    public static HashSet<string> ComputeFilteredKeys(
        IReadOnlyList<ProfiledMeasureType> measures,
        PatientCqlInput input)
    {
        if (measures == null || measures.Count == 0 || input == null)
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var applicableExclusions = new List<HashSet<string>>();
        foreach (var profile in Profiles)
        {
            if (!profile.AppliesToAny(measures))
                continue;
            applicableExclusions.Add(profile.ComputeExcludedKeys(input));
        }

        if (applicableExclusions.Count == 0)
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Intersect: keep only keys that every applicable profile excludes.
        var intersection = new HashSet<string>(applicableExclusions[0], StringComparer.OrdinalIgnoreCase);
        for (var i = 1; i < applicableExclusions.Count; i++)
            intersection.IntersectWith(applicableExclusions[i]);

        return intersection;
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
        IReadOnlyList<ConditionContext> Conditions);

    public interface ICqlFilterProfile
    {
        bool AppliesToAny(IReadOnlyList<ProfiledMeasureType> measures);
        HashSet<string> ComputeExcludedKeys(PatientCqlInput input);
    }

    private abstract class ConditionFilterProfileBase : ICqlFilterProfile
    {
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
}
