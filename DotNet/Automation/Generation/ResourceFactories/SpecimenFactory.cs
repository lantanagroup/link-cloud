using Hl7.Fhir.Model;
using static LantanaGroup.Automation.Generation.ResourceFactories.FhirConceptFactory;

namespace LantanaGroup.Automation.Generation.ResourceFactories;

public static class SpecimenFactory
{
    /// <summary>Generate a Specimen from the pool using a seed index, tracking produced IDs.</summary>
    public static Specimen Generate(
        string id, string patientId, DateTime collected, int seed,
        List<string> specimenIds, string collectorPractitionerId)
    {
        specimenIds.Add(id);
        var v = FhirGenerationCodes.Specimens[seed % FhirGenerationCodes.Specimens.Length];
        return Create(id, patientId, collected, seed,
                      v.TypeCode, v.TypeDisplay, v.TypeSystem,
                      v.ContainerCode, v.ContainerDisplay,
                      v.CollectionMethod, v.BodySiteCode, v.BodySiteDisplay,
                      collectorPractitionerId);
    }

    /// <summary>Create a Specimen with caller-supplied type values.</summary>
    public static Specimen Create(
        string id,
        string patientId,
        DateTime collected,
        int seed,
        string typeCode,
        string typeDisplay,
        string typeSystem,
        string containerCode,
        string containerDisplay,
        string collectionMethod,
        string bodySiteCode,
        string bodySiteDisplay,
        string collectorPractitionerId)
    {
        var volumeMl = 2.0 + seed % 8;

        return new Specimen
        {
            Id = id,
            Status = Specimen.SpecimenStatus.Available,
            Type = new CodeableConcept { Coding = [new Coding(typeSystem, typeCode, typeDisplay)], Text = typeDisplay },
            Subject = Ref($"Patient/{patientId}"),
            ReceivedTimeElement = new FhirDateTime(collected.AddMinutes(30 + seed % 60)),
            Collection = new Specimen.CollectionComponent
            {
                // FHIR R4: Specimen.collection.collector is Reference(Practitioner|PractitionerRole) only
                Collector = Ref($"Practitioner/{collectorPractitionerId}", "Collecting Practitioner"),
                Collected = new FhirDateTime(collected),
                Quantity = new Quantity
                {
                    Value = (decimal)volumeMl,
                    Unit = "mL",
                    System = "http://unitsofmeasure.org",
                    Code = "mL"
                },
                Method = CC("http://snomed.info/sct",
                              seed % 3 == 0 ? "28520004" : seed % 3 == 1 ? "73904009" : "82078001",
                              collectionMethod),
                BodySite = Snomed(bodySiteCode, bodySiteDisplay)
            },
            Container =
            [
                new Specimen.ContainerComponent
                {
                    Type             = CC("http://terminology.hl7.org/CodeSystem/v2-0487", containerCode, containerDisplay),
                    SpecimenQuantity = new Quantity { Value = (decimal)volumeMl, Unit = "mL", System = "http://unitsofmeasure.org", Code = "mL" }
                }
            ],
            Processing =
            [
                new Specimen.ProcessingComponent
                {
                    Description = "Standard laboratory processing",
                    Procedure   = CC("http://terminology.hl7.org/CodeSystem/v2-0373", "ROUT", "Routine"),
                    Time        = new FhirDateTime(collected.AddMinutes(45 + seed % 30))
                }
            ]
        };
    }
}
