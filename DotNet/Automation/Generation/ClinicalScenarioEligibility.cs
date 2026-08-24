namespace LantanaGroup.Automation.Generation;
public static class ClinicalScenarioEligibility
{
    /// <summary>
    /// Scenario IDs that qualify for the Glycemic Control / Hypoglycemic measure.
    /// DKA and Diabetic Hypoglycemia scenarios.
    /// </summary>
    private static readonly HashSet<Guid> HypoglycemicScenarioIds =
    [
        ClinicalScenarioIds.DiabeticKetoacidosis,
        ClinicalScenarioIds.DiabeticHypoglycemia,
    ];

    public static IReadOnlyList<string> GetEligibleScenarioIds(
        IReadOnlyList<ProfiledMeasureType> measures,
        MeasureEligibility eligibility)
    {
        return FhirGenerationCodes.ClinicalScenarios
            .Where(s =>
            {
                var qualifiesAll = QualifiesForAllMeasures(s, measures);
                return eligibility == MeasureEligibility.Qualifying ? qualifiesAll : !qualifiesAll;
            })
            .Select(s => s.ScenarioId.ToString())
            .ToList();
    }

    public static bool QualifiesForAllMeasures(
        FhirGenerationCodes.ClinicalScenarioDefinition scenario,
        IReadOnlyList<ProfiledMeasureType> measures)
    {
        if (measures == null || measures.Count == 0)
            return true;

        return measures.All(m => QualifiesForMeasure(scenario, m));
    }

    public static bool QualifiesForMeasure(
        FhirGenerationCodes.ClinicalScenarioDefinition scenario,
        ProfiledMeasureType measure)
    {
        return measure switch
        {
            // TODO(scenarios): every current story is an inpatient admission, so ACH
            // Qualifying/NonQualifying both cannot offer a distinct palette. Add
            // outpatient / observation / ED-only (and other ACH-IP-failing) scenarios
            // so an ACH non-qualifying cohort has selectable stories. Until then
            // GetEligibleScenarioIds(ACH, NonQualifying) is empty.
            ProfiledMeasureType.NhsnAcuteCareHospitalMonthlyInitialPopulation => true,
            ProfiledMeasureType.NhsnAcuteCareHospitalDailyInitialPopulation => true,
            ProfiledMeasureType.NhsnGlycemicControlHypoglycemicInitialPopulation => HypoglycemicScenarioIds.Contains(scenario.ScenarioId),
            _ => false
        };
    }
}
