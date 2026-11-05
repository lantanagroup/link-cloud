using LantanaGroup.Link.Census.Application.Models.Enums;
using LantanaGroup.Link.Census.Domain.Entities.POI;
using LantanaGroup.Link.Shared.Application.Models.Kafka;

namespace LantanaGroup.Link.Census.Application.Models.Payloads.Adt;

/// <summary>
/// Change an outpatient to an inpatient: the new class in PV1-2 is applied with the details common to every message.
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
