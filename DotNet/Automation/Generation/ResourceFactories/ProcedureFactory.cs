using Hl7.Fhir.Model;
using static LantanaGroup.Automation.Generation.ResourceFactories.FhirConceptFactory;

namespace LantanaGroup.Automation.Generation.ResourceFactories;

public static class ProcedureFactory
{
    /// <summary>Generate a Procedure from the pool using a seed index.</summary>
    public static Procedure Generate(
        string id, string patientId, string encounterId,
        DateTime performed, int seed, string practId,
        string locationId, string orgId,
        List<string> conditionIds)
    {
        var v = FhirGenerationCodes.Procedures[seed % FhirGenerationCodes.Procedures.Length];
        return Create(id, patientId, encounterId, performed, seed, practId, locationId, orgId,
                      v.Code, v.Display, v.BodySiteCode, v.BodySiteDisplay,
                      v.OutcomeCode, v.OutcomeDisplay,
                      conditionIds.Count > 0 ? conditionIds[seed % conditionIds.Count] : null);
    }

    /// <summary>Create a Procedure with caller-supplied values.</summary>
    public static Procedure Create(
        string id,
        string patientId,
        string encounterId,
        DateTime performed,
        int seed,
        string practId,
        string locationId,
        string orgId,
        string snomedCode,
        string display,
        string bodySiteCode,
        string bodySiteDisplay,
        string outcomeCode,
        string outcomeDisplay,
        string? reasonConditionId = null)
    {
        var duration = TimeSpan.FromMinutes(30 + seed % 180);

        var procedure = new Procedure
        {
            Id = id,
            Status = EventStatus.Completed,
            Code = Snomed(snomedCode, display),
            Subject = Ref($"Patient/{patientId}"),
            Encounter = Ref($"Encounter/{encounterId}"),
            Performed = new Period
            {
                StartElement = new FhirDateTime(performed),
                EndElement = new FhirDateTime(performed.Add(duration))
            },
            Performer =
            [
                new Procedure.PerformerComponent
                {
                    Function   = CC("http://snomed.info/sct", "59058001", "Surgeon"),
                    Actor      = Ref($"Practitioner/{practId}", "Performing Physician"),
                    OnBehalfOf = Ref($"Organization/{orgId}")
                }
            ],
            Location = Ref($"Location/{locationId}"),
            BodySite = [Snomed(bodySiteCode, bodySiteDisplay)],
            Outcome = Snomed(outcomeCode, outcomeDisplay),
        };

        if (reasonConditionId != null)
            procedure.ReasonReference = [Ref($"Condition/{reasonConditionId}")];

        return procedure;
    }
}
