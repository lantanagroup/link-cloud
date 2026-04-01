using Hl7.Fhir.Model;
using static LantanaGroup.Link.Automation.Generation.ResourceFactories.FhirConceptFactory;

namespace LantanaGroup.Link.Automation.Generation.ResourceFactories;

public static class DiagnosticReportFactory
{
    /// <summary>Generate a DiagnosticReport from the pool using a seed index.</summary>
    public static DiagnosticReport Generate(
        string id, string patientId, string encounterId,
        DateTime effective, int seed,
        List<string> observationIds, List<string> specimenIds,
        string practId)
    {
        var v = FhirGenerationCodes.DiagnosticReports[seed % FhirGenerationCodes.DiagnosticReports.Length];

        var resultRefs = observationIds.Count > 0
            ? observationIds
                .Skip(seed % Math.Max(1, observationIds.Count))
                .Take(5)
                .Select(oid => Ref($"Observation/{oid}"))
                .ToList()
            : new List<ResourceReference>();

        var specimenRefs = specimenIds.Count > 0
            ? new List<ResourceReference> { Ref($"Specimen/{specimenIds[seed % specimenIds.Count]}") }
            : new List<ResourceReference>();

        return Create(id, patientId, encounterId, effective, practId,
                      v.Code, v.Display, v.CategoryCode, v.CategoryDisplay,
                      resultRefs, specimenRefs);
    }

    /// <summary>Create a DiagnosticReport with caller-supplied values.</summary>
    public static DiagnosticReport Create(
        string id,
        string patientId,
        string encounterId,
        DateTime effective,
        string practId,
        string loincCode,
        string display,
        string categoryCode,
        string categoryDisplay,
        List<ResourceReference>? resultRefs = null,
        List<ResourceReference>? specimenRefs = null) => new()
        {
            Id = id,
            Status = DiagnosticReport.DiagnosticReportStatus.Final,
            Category =
        [
            new CodeableConcept
            {
                Coding = [new Coding("http://terminology.hl7.org/CodeSystem/v2-0074", categoryCode, categoryDisplay)],
                Text   = categoryDisplay
            }
        ],
            Code = Loinc(loincCode, display),
            Subject = Ref($"Patient/{patientId}"),
            Encounter = Ref($"Encounter/{encounterId}"),
            Effective = new FhirDateTime(effective),
            IssuedElement = new Instant(new DateTimeOffset(effective.AddHours(2))),
            Performer = [Ref($"Practitioner/{practId}", "Interpreting Physician")],
            ResultsInterpreter = [Ref($"Practitioner/{practId}", "Interpreting Physician")],
            Specimen = specimenRefs ?? [],
            Result = resultRefs ?? [],
            Conclusion = $"Results reviewed and interpreted. See individual observation values for findings."
        };
}
