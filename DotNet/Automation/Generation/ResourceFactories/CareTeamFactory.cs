using Hl7.Fhir.Model;
using static LantanaGroup.Automation.Generation.ResourceFactories.FhirConceptFactory;

namespace LantanaGroup.Automation.Generation.ResourceFactories;

public static class CareTeamFactory
{
    /// <summary>Generate a CareTeam using pool data.</summary>
    public static CareTeam Generate(string id, string patientId, string encounterId,
        string attendingPractId, DateTime period, string orgId) =>
        Create(id, patientId, encounterId, attendingPractId, period, orgId);

    /// <summary>Create a CareTeam with caller-supplied values.</summary>
    public static CareTeam Create(string id, string patientId, string encounterId,
        string attendingPractId, DateTime period, string orgId) => new()
        {
            Id = id,
            Status = CareTeam.CareTeamStatus.Active,
            Name = $"Inpatient Care Team",
            Subject = Ref($"Patient/{patientId}"),
            Encounter = Ref($"Encounter/{encounterId}"),
            Period = new Period { StartElement = new FhirDateTime(period) },
            Participant =
        [
            new CareTeam.ParticipantComponent
            {
                Role   = [CC("http://snomed.info/sct", "17561000", "Cardiologist")],
                Member = Ref($"Practitioner/{attendingPractId}", "Attending Physician"),
                Period = new Period { StartElement = new FhirDateTime(period) }
            }
        ],
            ReasonCode = [CC("http://snomed.info/sct", "305906004", "Admission to ward")],
            ManagingOrganization = [Ref($"Organization/{orgId}", "General Test Hospital")],
            Note = [new Annotation { Text = new Markdown("Multidisciplinary inpatient care team coordinating admission management.") }]
        };
}
