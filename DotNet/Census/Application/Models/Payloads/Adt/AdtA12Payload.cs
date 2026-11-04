using LantanaGroup.Link.Census.Application.Models.Enums;
using LantanaGroup.Link.Census.Domain.Entities.POI;
using LantanaGroup.Link.Shared.Application.Models.Kafka;

namespace LantanaGroup.Link.Census.Application.Models.Payloads.Adt;

/// <summary>
/// Cancel transfer: PV1-3 carries the location the patient returns to, applied with the details common to every message.
/// </summary>
public class AdtA12Payload : AdtPayload
{
    public override EventType EventType => EventType.A12;

    public AdtA12Payload()
    {
    }

    public AdtA12Payload(AdtEventEntry entry) : base(entry)
    {
    }

    protected override void ApplyToEncounter(PatientEncounter patientEncounter)
    {
        patientEncounter.EncounterStatus = InProgressStatus;
    }
}
