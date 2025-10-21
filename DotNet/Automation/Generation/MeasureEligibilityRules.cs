namespace LantanaGroup.Automation.Generation;

/// <summary>
/// Encodes the eligibility dependency graph between measures and validates
/// cohort configurations against impossible combinations.
/// <para>
/// Rule: Hypo requires ACH. You cannot fail ACH but pass Hypo.
/// </para>
/// <para>
/// Lives in the Automation library alongside <see cref="PatientCohortDefinition"/>
/// and <see cref="ClinicalScenarioEligibility"/> so both Automation.UI and
/// BackendE2ETests use identical validation logic.
/// </para>
/// </summary>
public static class MeasureEligibilityRules
{
    /// <summary>
    /// Measures that are prerequisites for other measures.
    /// Key = dependent measure, Value = list of prerequisite measures.
    /// If you qualify for the key, you MUST also qualify for every value.
    /// </summary>
    private static readonly Dictionary<ProfiledMeasureType, ProfiledMeasureType[]> Prerequisites = new()
    {
        [ProfiledMeasureType.NhsnGlycemicControlHypoglycemicInitialPopulation] =
        [
            ProfiledMeasureType.NhsnAcuteCareHospitalMonthlyInitialPopulation,
        ],
    };

    /// <summary>
    /// Validates a per-measure eligibility map for a cohort against the known
    /// measure dependency rules.
    /// Returns a list of error messages. Empty list = valid.
    /// </summary>
    public static List<string> Validate(
        IReadOnlyList<ProfiledMeasureType> selectedMeasures,
        PatientCohortDefinition cohort)
    {
        var errors = new List<string>();
        foreach (var measure in selectedMeasures)
        {
            if (cohort.GetEligibility(measure) != MeasureEligibility.Qualifying)
                continue;

            if (!Prerequisites.TryGetValue(measure, out var prereqs))
                continue;

            foreach (var prereq in prereqs)
            {
                if (!selectedMeasures.Contains(prereq))
                    continue;

                if (cohort.GetEligibility(prereq) == MeasureEligibility.NonQualifying)
                {
                    errors.Add(
                        $"Cannot qualify for {ProfiledMeasureCatalog.GetDisplayName(measure)} " +
                        $"while failing {ProfiledMeasureCatalog.GetDisplayName(prereq)}. " +
                        $"{ProfiledMeasureCatalog.GetDisplayName(prereq)} is a prerequisite.");
                }
            }
        }

        return errors;
    }

    /// <summary>
    /// Validates all cohorts in a run configuration.
    /// Returns a flat list of error messages. Empty = all valid.
    /// </summary>
    public static List<string> ValidateAll(
        IReadOnlyList<ProfiledMeasureType> selectedMeasures,
        IReadOnlyList<PatientCohortDefinition> cohorts)
    {
        var errors = new List<string>();
        for (var i = 0; i < cohorts.Count; i++)
        {
            var cohortErrors = Validate(selectedMeasures, cohorts[i]);
            foreach (var error in cohortErrors)
                errors.Add($"Cohort {i + 1}: {error}");
        }

        return errors;
    }

    /// <summary>
    /// For a given measure, returns which eligibility values are allowed given
    /// the current eligibility selections on other measures.
    /// Used by UI to disable impossible dropdown options dynamically.
    /// </summary>
    public static IReadOnlyList<MeasureEligibility> GetAllowedEligibilities(
        ProfiledMeasureType measure,
        IReadOnlyList<ProfiledMeasureType> selectedMeasures,
        IReadOnlyDictionary<ProfiledMeasureType, MeasureEligibility> currentSelections)
    {
        var allowed = new List<MeasureEligibility> { MeasureEligibility.Qualifying, MeasureEligibility.NonQualifying };

        // If this measure has prerequisites, and any prereq is NonQualifying,
        // then Qualifying is not allowed for this measure.
        if (Prerequisites.TryGetValue(measure, out var prereqs))
        {
            foreach (var prereq in prereqs)
            {
                if (selectedMeasures.Contains(prereq)
                    && currentSelections.TryGetValue(prereq, out var prereqEligibility)
                    && prereqEligibility == MeasureEligibility.NonQualifying)
                {
                    allowed.Remove(MeasureEligibility.Qualifying);
                    break;
                }
            }
        }

        // If this measure is a prerequisite for another that is Qualifying,
        // then NonQualifying is not allowed for this measure.
        foreach (var (dependent, deps) in Prerequisites)
        {
            if (!deps.Contains(measure))
                continue;
            if (!selectedMeasures.Contains(dependent))
                continue;
            if (currentSelections.TryGetValue(dependent, out var depEligibility)
                && depEligibility == MeasureEligibility.Qualifying)
            {
                allowed.Remove(MeasureEligibility.NonQualifying);
                break;
            }
        }

        return allowed;
    }
}
