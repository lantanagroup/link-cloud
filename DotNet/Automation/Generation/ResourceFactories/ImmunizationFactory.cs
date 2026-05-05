using Hl7.Fhir.Model;
using static LantanaGroup.Automation.Generation.ResourceFactories.FhirConceptFactory;

namespace LantanaGroup.Automation.Generation.ResourceFactories;

public static class ImmunizationFactory
{
    /// <summary>Generate an Immunization from the pool using a seed index.</summary>
    public static Immunization Generate(
        string id, string patientId, string encounterId,
        DateTime occurrence, int seed, string locationId, string organizationId)
    {
        var v = FhirGenerationCodes.Immunizations[seed % FhirGenerationCodes.Immunizations.Length];
        return Create(id, patientId, encounterId, occurrence, seed, locationId, organizationId, v.CvxCode, v.Display, v.DoseML);
    }

    /// <summary>Create an Immunization with caller-supplied vaccine values.</summary>
    public static Immunization Create(
        string id,
        string patientId,
        string encounterId,
        DateTime occurrence,
        int seed,
        string locationId,
        string organizationId,
        string cvxCode,
        string display,
        double doseMl) => new()
        {
            Id = id,
            Status = Immunization.ImmunizationStatusCodes.Completed,
            VaccineCode = new CodeableConcept
            {
                Coding =
            [
                new Coding("http://hl7.org/fhir/sid/cvx",      cvxCode, display),
                new Coding("http://hl7.org/fhir/sid/ndc",      $"NDC-{cvxCode}", display)
            ],
                Text = display
            },
            Patient = Ref($"Patient/{patientId}"),
            Encounter = Ref($"Encounter/{encounterId}"),
            Occurrence = new FhirDateTime(occurrence),
            PrimarySource = true,
            LotNumber = $"LOT{seed * 7 + 1001:D5}",
            ExpirationDate = occurrence.AddYears(1).ToString("yyyy-MM-dd"),
            Location = Ref($"Location/{locationId}"),
            Performer =
        [
            new Immunization.PerformerComponent
            {
                Function = CC("http://terminology.hl7.org/CodeSystem/v2-0443", "AP", "Administering Provider"),
                Actor    = Ref($"Organization/{organizationId}", "General Test Hospital")
            }
        ],
            DoseQuantity = new Quantity { Value = (decimal)doseMl, Unit = "mL", System = "http://unitsofmeasure.org", Code = "mL" },
            Site = CC("http://terminology.hl7.org/CodeSystem/v3-ActSite",
                           seed % 2 == 0 ? "LA" : "RA",
                           seed % 2 == 0 ? "Left arm" : "Right arm"),
            Route = CC("http://terminology.hl7.org/CodeSystem/v3-RouteOfAdministration", "IM", "Injection, intramuscular"),
            ProtocolApplied =
        [
            new Immunization.ProtocolAppliedComponent
            {
                Series      = $"Dose {seed % 2 + 1}",
                DoseNumber  = new FhirString($"{seed % 2 + 1}"),
                SeriesDoses = new FhirString("2")
            }
        ]
        };
}
