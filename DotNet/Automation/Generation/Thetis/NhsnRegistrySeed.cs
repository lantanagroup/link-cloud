using Thetis.Generation.Abstractions;

namespace LantanaGroup.Automation.Generation.Thetis;

/// <summary>
/// In-memory clinical lookup seed for library-hosted Thetis generation.
/// Ports primary diagnoses from <see cref="FhirGenerationCodes"/> plus hypo insulin.
/// </summary>
public static class NhsnRegistrySeed
{
    public static IReadOnlyList<ConditionLookupRecord> Conditions { get; } =
        FhirGenerationCodes.ClinicalScenarios.Select(s => new ConditionLookupRecord
        {
            Id = s.ScenarioId,
            ConditionSystem = "http://snomed.info/sct",
            ConditionCode = s.PrimaryDxSnomed,
            ConditionDisplay = s.PrimaryDxDisplay,
            Category = "encounter-diagnosis",
            SelectionWeight = 1
        }).ToList();

    public static IReadOnlyList<MedicationLookupRecord> Medications { get; } =
    [
        HypoInsulinFor(ClinicalScenarioIds.DiabeticHypoglycemia, "421725003", "Diabetes mellitus type 2 with hypoglycemia"),
        HypoInsulinFor(ClinicalScenarioIds.DiabeticKetoacidosis, "420422005", "Diabetic ketoacidosis")
    ];

    private static MedicationLookupRecord HypoInsulinFor(Guid id, string triggerCode, string triggerDisplay) => new()
    {
        Id = id,
        TriggerType = "condition",
        TriggerSystem = "http://snomed.info/sct",
        TriggerCode = triggerCode,
        TriggerDisplay = triggerDisplay,
        MedicationSystem = "http://www.nlm.nih.gov/research/umls/rxnorm",
        MedicationCode = "274783",
        MedicationDisplay = "insulin glargine",
        SelectionWeight = 1
    };
}
