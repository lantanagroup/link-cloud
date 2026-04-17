namespace LantanaGroup.Automation.Generation;

/// <summary>
/// Simulates measure-specific CQL SDE <c>where</c>-clause filtering at the individual-resource level.
///
/// Type-level reachability (<c>[ResourceType]</c>) is handled elsewhere. This simulator focuses on
/// per-resource exclusions where CQL retrieves a type but includes only rows matching additional
/// predicates (status/date/category/reference constraints).
/// </summary>
public static class CqlFilterSimulator
{
    private static readonly IReadOnlyList<ICqlFilterProfile> Profiles =
    [
        new AchConditionFilterProfile(),
        new HypoglycemicConditionFilterProfile()
    ];

    /// <summary>
    /// Computes resource keys excluded by measure-specific CQL filters for a patient.
    /// The result is a union across all selected measures (multi-measure runs).
    /// </summary>
    public static HashSet<string> ComputeFilteredKeys(
        IReadOnlyList<ProfiledMeasureType> measures,
        string patientId,
        string encounterId,
        DateTime encStart,
        DateTime encEnd,
        int scenarioIdx,
        int baseSeed,
        int patientOrdinal,
        int totalResourcesPerPatient,
        FhirGenerationConfig? config)
    {
        if (measures == null || measures.Count == 0)
            return [];

        var context = new PatientFilterContext(
            patientId,
            encounterId,
            encStart,
            encEnd,
            scenarioIdx,
            baseSeed,
            patientOrdinal,
            totalResourcesPerPatient,
            config,
            measures);

        var excluded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var profile in Profiles)
        {
            if (!profile.AppliesToAny(measures))
                continue;

            foreach (var key in profile.ComputeExcludedKeys(context))
                excluded.Add(key);
        }

        return excluded;
    }

    public sealed record PatientFilterContext(
        string PatientId,
        string EncounterId,
        DateTime EncounterStart,
        DateTime EncounterEnd,
        int ScenarioIndex,
        int BaseSeed,
        int PatientOrdinal,
        int TotalResourcesPerPatient,
        FhirGenerationConfig? Config,
        IReadOnlyList<ProfiledMeasureType> Measures);

    public interface ICqlFilterProfile
    {
        bool AppliesToAny(IReadOnlyList<ProfiledMeasureType> measures);
        HashSet<string> ComputeExcludedKeys(PatientFilterContext context);
    }

    /// <summary>
    /// Base helper for profiles that filter Condition resources.
    /// Replays only the Condition branch of generation deterministically.
    /// </summary>
    private abstract class ConditionFilterProfileBase : ICqlFilterProfile
    {
        public abstract bool AppliesToAny(IReadOnlyList<ProfiledMeasureType> measures);

        protected abstract bool IncludeCondition(
            ConditionContext c,
            DateTime encounterEnd,
            string encounterId);

        public HashSet<string> ComputeExcludedKeys(PatientFilterContext context)
        {
            var distribution = (context.Config ?? new FhirGenerationConfig()).ResourceDistribution
                .Select(kv => (kv.Key, kv.Value)).ToArray();

            var condIndices = ScenarioResourceMap.GetMergedIndices(
                ScenarioResourceMap.UniversalConditionIndices,
                ScenarioResourceMap.ScenarioConditionIndices,
                context.ScenarioIndex,
                FhirGenerationCodes.Conditions.Length);

            var excluded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var resourceIndex = 0;

            foreach (var (resourceType, fraction) in distribution)
            {
                var count = Math.Max(1, (int)(context.TotalResourcesPerPatient * fraction));

                for (var i = 0; i < count; i++)
                {
                    resourceIndex++;
                    if (!string.Equals(resourceType, "Condition", StringComparison.Ordinal))
                        continue;

                    var seed = context.BaseSeed + (context.PatientOrdinal * 31 + i);
                    var resourceId = $"{context.PatientId}-{FhirBundleGenerator.AbbreviateResourceType(resourceType)}-{resourceIndex:D3}";
                    var offset = TimeSpan.FromMinutes((double)i / Math.Max(count, 1) * (context.EncounterEnd - context.EncounterStart).TotalMinutes);
                    var effectiveDate = context.EncounterStart.Add(offset);

                    var poolIdx = ScenarioResourceMap.PickIndex(condIndices, seed, FhirGenerationCodes.Conditions.Length);
                    var v = FhirGenerationCodes.Conditions[poolIdx];
                    var isActive = seed % 5 != 0;

                    var categories = new List<string> { "problem-list-item" };
                    if (v.Category == "encounter-diagnosis") categories.Add("encounter-diagnosis");
                    else if (v.Category == "health-concern") categories.Add("health-concern");

                    var condition = new ConditionContext(
                        resourceId,
                        isActive,
                        effectiveDate.Date,
                        context.EncounterId,
                        categories);

                    if (!IncludeCondition(condition, context.EncounterEnd, context.EncounterId))
                        excluded.Add($"Condition/{resourceId}");
                }
            }

            return excluded;
        }
    }

    /// <summary>
    /// ACH Monthly + ACH Daily SDE Condition semantics:
    /// - problem-list-item requires active + recordedDate before encounter end
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
                && string.Equals(c.EncounterReference, encounterId, StringComparison.OrdinalIgnoreCase))
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
    ///
    /// We approximate overlap using recordedDate within encounter period date window.
    /// </summary>
    private sealed class HypoglycemicConditionFilterProfile : ConditionFilterProfileBase
    {
        public override bool AppliesToAny(IReadOnlyList<ProfiledMeasureType> measures) =>
            measures.Contains(ProfiledMeasureType.NhsnGlycemicControlHypoglycemicInitialPopulation);

        protected override bool IncludeCondition(ConditionContext c, DateTime encounterEnd, string encounterId)
        {
            // Generated Conditions are all encounter-scoped with onset in the encounter timeline.
            // Treat recordedDate <= encounter end date as overlapping for prediction purposes.
            return c.RecordedDate <= encounterEnd.Date;
        }
    }

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
}
