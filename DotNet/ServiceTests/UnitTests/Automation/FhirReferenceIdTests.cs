using Hl7.Fhir.Model;
using LantanaGroup.Automation.Generation;

namespace UnitTests.Automation;

[Trait("Category", "UnitTests")]
public class FhirReferenceIdTests
{
    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData("   ", "")]
    [InlineData("HypoInsulinGlargine", "HypoInsulinGlargine")]
    [InlineData("Medication/HypoInsulinGlargine", "HypoInsulinGlargine")]
    [InlineData("https://server.example/fhir/Medication/HypoInsulinGlargine", "HypoInsulinGlargine")]
    [InlineData("http://localhost:8080/fhir/Medication/HypoInsulinGlargine", "HypoInsulinGlargine")]
    [InlineData("https://server.example/fhir/Medication/HypoInsulinGlargine/_history/2", "HypoInsulinGlargine")]
    [InlineData("Medication/HypoInsulinGlargine/_history/2", "HypoInsulinGlargine")]
    [InlineData("https://server.example/fhir/Medication/HypoInsulinGlargine?_format=json", "HypoInsulinGlargine")]
    [InlineData("#contained-med", "contained-med")]
    public void FromReference_returns_logical_resource_id(string? reference, string expected)
        => Assert.Equal(expected, FhirReferenceId.FromReference(reference));

    [Fact]
    public void ExtractFromEntries_resolves_medication_codes_from_absolute_fhir_url()
    {
        var entries = new List<Bundle.EntryComponent>
        {
            Entry(new Patient { Id = "P1" }),
            Entry(new Encounter
            {
                Id = "E1",
                Status = Encounter.EncounterStatus.Finished,
                Class = new Coding("http://terminology.hl7.org/CodeSystem/v3-ActCode", "IMP"),
                Period = new Period { Start = "2026-07-12T06:00:00Z", End = "2026-07-29T14:00:00Z" },
                Subject = new ResourceReference("Patient/P1")
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
                Subject = new ResourceReference("Patient/P1"),
                Medication = new ResourceReference(
                    "https://fhir.example.org/r4/Medication/HypoInsulinGlargine"),
                AuthoredOn = "2026-07-20T12:00:00Z"
            })
        };

        var input = CqlFilterInputExtractor.ExtractFromEntries("P1", entries);
        Assert.NotNull(input);
        var request = Assert.Single(input.MedicationRequests);
        Assert.Contains("274783", request.MedicationCodes);
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
