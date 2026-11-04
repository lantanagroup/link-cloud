using LantanaGroup.Link.Census.Application.Models.Enums;
using LantanaGroup.Link.Census.Domain.Entities.POI;
using LantanaGroup.Link.Shared.Application.Models.Kafka;

namespace LantanaGroup.Link.Census.Application.Models.Payloads.Adt;

/// <summary>
/// Discharge.
/// </summary>
public class AdtA03Payload : AdtPayload
{
    public override EventType EventType => EventType.A03;

    public AdtA03Payload()
    {
    }

    public AdtA03Payload(AdtEventEntry entry) : base(entry)
    {
    }

    protected override void ApplyToEncounter(PatientEncounter patientEncounter)
    {
        patientEncounter.EncounterStatus = FinishedStatus;
        patientEncounter.DischargeDate = DischargeDateTime ?? MessageDateTime ?? DateTime.UtcNow;
    }
}
