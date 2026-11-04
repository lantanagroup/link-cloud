using LantanaGroup.Link.Census.Application.Models.Enums;
using LantanaGroup.Link.Census.Domain.Entities.POI;
using LantanaGroup.Link.Shared.Application.Models.Kafka;

namespace LantanaGroup.Link.Census.Application.Models.Payloads.Adt;

/// <summary>
/// Cancel discharge: the encounter is open again.
/// </summary>
public class AdtA13Payload : AdtPayload
{
    public override EventType EventType => EventType.A13;

    public AdtA13Payload()
    {
    }

    public AdtA13Payload(AdtEventEntry entry) : base(entry)
    {
    }

    protected override void ApplyToEncounter(PatientEncounter patientEncounter)
    {
        patientEncounter.EncounterStatus = InProgressStatus;
        patientEncounter.DischargeDate = null;
    }
}
