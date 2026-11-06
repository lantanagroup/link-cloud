using LantanaGroup.Link.Census.Application.Models.Enums;
using LantanaGroup.Link.Census.Domain.Entities.POI;
using LantanaGroup.Link.Shared.Application.Models.Kafka;

namespace LantanaGroup.Link.Census.Application.Models.Payloads.Adt;

/// <summary>
/// Change an outpatient to an inpatient: nothing beyond the details common to every message. PV1-2 is kept in the payload.
/// </summary>
public class AdtA06Payload : AdtPayload
{
    public override EventType EventType => EventType.A06;

    public AdtA06Payload()
    {
    }

    public AdtA06Payload(AdtEventEntry entry) : base(entry)
    {
    }

    protected override void ApplyToEncounter(PatientEncounter patientEncounter)
    {
    }
}
