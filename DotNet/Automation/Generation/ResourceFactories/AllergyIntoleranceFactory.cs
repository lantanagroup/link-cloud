using Hl7.Fhir.Model;
using static LantanaGroup.Automation.Generation.ResourceFactories.FhirConceptFactory;

namespace LantanaGroup.Automation.Generation.ResourceFactories;

public static class AllergyIntoleranceFactory
{
    /// <summary>Generate an AllergyIntolerance from the pool using a seed index.</summary>
    public static AllergyIntolerance Generate(
        string id, string patientId, DateTime recorded, int seed, string recorderPractitionerId)
    {
        var v = FhirGenerationCodes.Allergies[seed % FhirGenerationCodes.Allergies.Length];
        return Create(id, patientId, recorded, seed, v.Code, v.Display,
                      v.ManifestationCode, v.ManifestationDisplay,
                      v.Severity, v.ExposureRoute,
                      recorderPractitionerId);
    }

    /// <summary>Create an AllergyIntolerance with caller-supplied substance values.</summary>
    public static AllergyIntolerance Create(
        string id,
        string patientId,
        DateTime recorded,
        int seed,
        string snomedCode,
        string display,
        string manifestationCode,
        string manifestationDisplay,
        string severityStr,
        string exposureRouteCode,
        string recorderPractitionerId)
    {
        var categories = new[] { AllergyIntolerance.AllergyIntoleranceCategory.Medication, AllergyIntolerance.AllergyIntoleranceCategory.Food, AllergyIntolerance.AllergyIntoleranceCategory.Environment, AllergyIntolerance.AllergyIntoleranceCategory.Biologic };
        var criticalities = new[] { AllergyIntolerance.AllergyIntoleranceCriticality.Low, AllergyIntolerance.AllergyIntoleranceCriticality.High, AllergyIntolerance.AllergyIntoleranceCriticality.UnableToAssess };
        var severity = severityStr switch
        {
            "severe" => AllergyIntolerance.AllergyIntoleranceSeverity.Severe,
            "moderate" => AllergyIntolerance.AllergyIntoleranceSeverity.Moderate,
            _ => AllergyIntolerance.AllergyIntoleranceSeverity.Mild
        };

        return new AllergyIntolerance
        {
            Id = id,
            ClinicalStatus = CC("http://terminology.hl7.org/CodeSystem/allergyintolerance-clinical", "active", "Active"),
            VerificationStatus = CC("http://terminology.hl7.org/CodeSystem/allergyintolerance-verification", "confirmed", "Confirmed"),
            Type = seed % 2 == 0 ? AllergyIntolerance.AllergyIntoleranceType.Allergy : AllergyIntolerance.AllergyIntoleranceType.Intolerance,
            Category = [categories[seed % categories.Length]],
            Criticality = criticalities[seed % criticalities.Length],
            Code = new CodeableConcept { Coding = [new Coding("http://snomed.info/sct", snomedCode, display)], Text = display },
            Patient = Ref($"Patient/{patientId}"),
            RecordedDateElement = new FhirDateTime(recorded),
            Recorder = Ref($"Practitioner/{recorderPractitionerId}", "Documenting Practitioner"),
            Reaction =
            [
                new AllergyIntolerance.ReactionComponent
                {
                    Substance     = new CodeableConcept { Coding = [new Coding("http://snomed.info/sct", snomedCode, display)], Text = display },
                    Manifestation = [Snomed(manifestationCode, manifestationDisplay)],
                    Severity      = severity,
                    ExposureRoute = CC("http://snomed.info/sct", exposureRouteCode, "Oral route"),
                    Note          = [new Annotation { Text = new Markdown($"Documented {severityStr} reaction to {display}.") }]
                }
            ]
        };
    }
}
