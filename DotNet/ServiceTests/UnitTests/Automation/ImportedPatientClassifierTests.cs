using Hl7.Fhir.Model;
using LantanaGroup.Automation.Generation;

namespace UnitTests.Automation;

[Trait("Category", "UnitTests")]
public class ImportedPatientClassifierTests
{
    [Fact]
    public void Hypo_qualifies_when_insulin_glargine_274783_is_medication_reference()
    {
        // Thetis HypoInsulinGlargine and FhirBundleGenerator's shared Hypo
        // medication use RxNorm 274783 on Medication, referenced from
        // MedicationRequest. A CodeableConcept-only classifier misses it
        // (Thetis dumps 10/11 predicted Hypo NQ while MeasureEval IP count was 1).
        var entries = new List<Bundle.EntryComponent>
        {
            Entry(new Encounter
            {
                Id = "E1",
                Status = Encounter.EncounterStatus.Finished,
                Class = new Coding("http://terminology.hl7.org/CodeSystem/v3-ActCode", "IMP"),
                Period = new Period { Start = "2026-08-08T14:10:00-05:00", End = "2026-08-27T18:10:00-05:00" }
            }),
            Entry(new Medication
            {
                Id = "HypoInsulinGlargine",
                Code = new CodeableConcept
                {
                    Coding = [new Coding("http://www.nlm.nih.gov/research/umls/rxnorm", "274783", "insulin glargine")]
                }
            }),
            Entry(new MedicationRequest
            {
                Id = "MR-hypo",
                Status = MedicationRequest.MedicationrequestStatus.Active,
                Intent = MedicationRequest.MedicationRequestIntent.Order,
                Medication = new ResourceReference("Medication/HypoInsulinGlargine"),
                AuthoredOn = "2026-08-08T14:10:00-05:00"
            })
        };

        var result = ImportedPatientClassifier.Classify(
            entries,
            [ProfiledMeasureType.NhsnGlycemicControlHypoglycemicInitialPopulation]);

        Assert.Equal(
            MeasureEligibility.Qualifying,
            result.MeasureEligibilities[ProfiledMeasureType.NhsnGlycemicControlHypoglycemicInitialPopulation]);
    }

    [Fact]
    public void DiabetesMedicationCodes_include_thetis_insulin_glargine_ingredient()
    {
        Assert.True(EncounterIpClassification.IsDiabetesMedicationCode("274783"));
        Assert.True(EncounterIpClassification.IsDiabetesMedicationCode("1116635"));
        Assert.True(EncounterIpClassification.IsDiabetesMedicationCode("311040"));
    }

    private static Bundle.EntryComponent Entry(Resource resource)
        => new()
        {
            Resource = resource,
            Request = new Bundle.RequestComponent
            {
                Method = Bundle.HTTPVerb.PUT,
                Url = $"{resource.TypeName}/{resource.Id}"
            }
        };
}
