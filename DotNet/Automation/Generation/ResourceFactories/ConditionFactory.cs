using Hl7.Fhir.Model;
using static LantanaGroup.Automation.Generation.ResourceFactories.FhirConceptFactory;

namespace LantanaGroup.Automation.Generation.ResourceFactories;

public static class ConditionFactory
{
    /// <summary>
    /// Create the primary admission diagnosis directly from scenario data.
    /// Always active, encounter-diagnosis category, rank 1.
    /// </summary>
    public static Condition CreatePrimary(
        string id,
        string patientId,
        string encounterId,
        DateTime onset,
        string snomedCode,
        string display,
        string icdCode) => new()
        {
            Id = id,
            ClinicalStatus = CC("http://terminology.hl7.org/CodeSystem/condition-clinical", "active", "Active"),
            VerificationStatus = CC("http://terminology.hl7.org/CodeSystem/condition-ver-status", "confirmed", "Confirmed"),
            Category =
        [
            CC("http://terminology.hl7.org/CodeSystem/condition-category",  "problem-list-item", "Problem List Item"),
            CC("http://terminology.hl7.org/CodeSystem/condition-category",  "encounter-diagnosis", "Encounter Diagnosis")
        ],
            Severity = CC("http://snomed.info/sct", "24484000", "Severe"),
            Code = new CodeableConcept
            {
                Coding =
            [
                new Coding("http://snomed.info/sct",            snomedCode, display),
                new Coding("http://hl7.org/fhir/sid/icd-10-cm", icdCode,    display)
            ],
                Text = display
            },
            Subject = Ref($"Patient/{patientId}"),
            Encounter = Ref($"Encounter/{encounterId}"),
            Onset = new FhirDateTime(onset),
            RecordedDate = onset.ToString("yyyy-MM-dd")
        };

    /// <summary>Generate a secondary/comorbid Condition from the pool using a seed index.</summary>
    public static Condition Generate(
        string id,
        string patientId,
        string encounterId,
        DateTime onset,
        DateTime abatement,
        int seed,
        List<string> conditionIds)
    {
        var v = FhirGenerationCodes.Conditions[seed % FhirGenerationCodes.Conditions.Length];
        conditionIds.Add(id);
        return Create(id, patientId, encounterId, onset, abatement, seed, v.Code, v.Display, v.IcdCode, v.Category);
    }

    /// <summary>Create a Condition with caller-supplied values; clinical status/severity derived from seed.</summary>
    public static Condition Create(
        string id,
        string patientId,
        string encounterId,
        DateTime onset,
        DateTime abatement,
        int seed,
        string snomedCode,
        string display,
        string icdCode,
        string categoryCode)
    {
        var isActive = seed % 5 != 0;
        var severityCodes = new[] { ("24484000", "Severe"), ("6736007", "Moderate"), ("255604002", "Mild") };
        var (sevCode, sevDisplay) = severityCodes[seed % severityCodes.Length];

        var categories = new List<CodeableConcept>
        {
            CC("http://terminology.hl7.org/CodeSystem/condition-category", "problem-list-item", "Problem List Item")
        };

        if (categoryCode == "encounter-diagnosis")
            categories.Add(CC("http://terminology.hl7.org/CodeSystem/condition-category", "encounter-diagnosis", "Encounter Diagnosis"));
        else if (categoryCode == "health-concern")
            categories.Add(CC("http://hl7.org/fhir/us/core/CodeSystem/condition-category", "health-concern", "Health Concern"));

        var condition = new Condition
        {
            Id = id,
            ClinicalStatus = CC("http://terminology.hl7.org/CodeSystem/condition-clinical", isActive ? "active" : "resolved", isActive ? "Active" : "Resolved"),
            VerificationStatus = CC("http://terminology.hl7.org/CodeSystem/condition-ver-status", "confirmed", "Confirmed"),
            Category = categories,
            Severity = CC("http://snomed.info/sct", sevCode, sevDisplay),
            Code = new CodeableConcept
            {
                Coding =
                [
                    new Coding("http://snomed.info/sct",            snomedCode, display),
                    new Coding("http://hl7.org/fhir/sid/icd-10-cm", icdCode,    display)
                ],
                Text = display
            },
            Subject = Ref($"Patient/{patientId}"),
            Encounter = Ref($"Encounter/{encounterId}"),
            Onset = new FhirDateTime(onset),
            RecordedDate = onset.ToString("yyyy-MM-dd")
        };

        if (!isActive)
            condition.Abatement = new FhirDateTime(abatement);

        return condition;
    }
}
