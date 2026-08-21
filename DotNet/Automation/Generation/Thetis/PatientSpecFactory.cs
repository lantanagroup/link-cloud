using Thetis.Generation.Abstractions;

namespace LantanaGroup.Automation.Generation.Thetis;

/// <summary>
/// Maps Automation scenario/profile knobs onto a Thetis <see cref="PatientGenerationSpec"/>.
/// The Engine compiler turns that spec into a DAG; this factory contains no node JSON.
/// </summary>
public static class PatientSpecFactory
{
    public const string HypoInsulinMedicationIdVar = "hypoInsulinMedicationId";

    public static PatientGenerationSpec From(
        PatientProfile profile,
        FhirGenerationCodes.ClinicalScenarioDefinition scenario,
        int observationCount = 10)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(scenario);

        var inpatient = profile.RequiresInpatientEncounter();
        var hypo = profile.RequiresHypoglycemicMedication();
        return FromScenario(scenario, inpatient, hypo, observationCount);
    }

    public static PatientGenerationSpec FromScenario(
        FhirGenerationCodes.ClinicalScenarioDefinition scenario,
        bool inpatient = true,
        bool hypo = false,
        int observationCount = 10)
    {
        ArgumentNullException.ThrowIfNull(scenario);

        return new PatientGenerationSpec
        {
            EncounterClass = inpatient ? "IMP" : "AMB",
            PrimaryConditionSnomed = scenario.PrimaryDxSnomed,
            PrimaryConditionDisplay = scenario.PrimaryDxDisplay,
            ConditionCategory = "encounter-diagnosis",
            GenerateLabWork = true,
            ObservationCount = Math.Max(1, observationCount),
            SpreadObservationsAcrossEncounter = inpatient,
            IncludeMedicationRequest = hypo,
            MedicationIdVar = hypo ? HypoInsulinMedicationIdVar : null
        };
    }
}
