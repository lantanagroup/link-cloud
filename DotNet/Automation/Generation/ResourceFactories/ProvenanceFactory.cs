using Hl7.Fhir.Model;
using static LantanaGroup.Automation.Generation.ResourceFactories.FhirConceptFactory;

namespace LantanaGroup.Automation.Generation.ResourceFactories;

public static class ProvenanceFactory
{
    /// <summary>Generate a Provenance with standard author + transmitter agents.</summary>
    public static Provenance Generate(
        string id, string patientId, string encounterId,
        DateTime recorded, string practId, string orgId) =>
        Create(id, patientId, encounterId, recorded, practId, orgId);

    /// <summary>Create a Provenance with caller-supplied values.</summary>
    public static Provenance Create(
        string id, string patientId, string encounterId,
        DateTime recorded, string practId, string orgId) => new()
        {
            Id = id,
            Target =
        [
            Ref($"Encounter/{encounterId}"),
            Ref($"Patient/{patientId}")
        ],
            RecordedElement = new Instant(new DateTimeOffset(recorded)),
            Occurred = new Period
            {
                StartElement = new FhirDateTime(recorded),
                EndElement = new FhirDateTime(recorded.AddMinutes(5))
            },
            Activity = CC("http://terminology.hl7.org/CodeSystem/v3-DataOperation", "CREATE", "create"),
            Agent =
        [
            new Provenance.AgentComponent
            {
                Type       = CC("http://terminology.hl7.org/CodeSystem/provenance-participant-type", "author", "Author"),
                Role       = [CC("http://terminology.hl7.org/CodeSystem/v3-RoleClass", "PROV", "healthcare provider")],
                Who        = Ref($"Practitioner/{practId}", "Authoring Clinician"),
                OnBehalfOf = Ref($"Organization/{orgId}", "General Test Hospital")
            },
            new Provenance.AgentComponent
            {
                Type       = CC("http://hl7.org/fhir/us/core/CodeSystem/us-core-provenance-participant-type", "transmitter", "Transmitter"),
                Who        = Ref($"Organization/{orgId}", "General Test Hospital")
            }
        ],
            Signature = []
        };
}
