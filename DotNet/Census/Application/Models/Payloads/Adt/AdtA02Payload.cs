using LantanaGroup.Link.Census.Application.Models.Enums;
using LantanaGroup.Link.Census.Domain.Entities.POI;
using LantanaGroup.Link.Shared.Application.Models.Kafka;

namespace LantanaGroup.Link.Census.Application.Models.Payloads.Adt;

/// <summary>
/// Transfer: the new location in PV1-3 is applied with the details common to every message.
/// </summary>
public class AdtA02Payload : AdtPayload
{
    public override EventType EventType => EventType.A02;

    public AdtA02Payload()
    {
    }

    public AdtA02Payload(AdtEventEntry entry) : base(entry)
    {
    }

    protected override void ApplyToEncounter(PatientEncounter patientEncounter)
    {
        patientEncounter.EncounterStatus = InProgressStatus;
    }
}
