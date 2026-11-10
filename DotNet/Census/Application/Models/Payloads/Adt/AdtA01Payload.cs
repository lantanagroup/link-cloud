using LantanaGroup.Link.Census.Application.Models.Enums;
using LantanaGroup.Link.Census.Domain.Entities.POI;
using LantanaGroup.Link.Shared.Application.Models.Kafka;

namespace LantanaGroup.Link.Census.Application.Models.Payloads.Adt;

/// <summary>
/// Admit/visit: the message carries the real admit date, which a stay first seen through a later message lacks.
/// </summary>
public class AdtA01Payload : AdtPayload
{
    public override EventType EventType => EventType.A01;

    public AdtA01Payload()
    {
    }

    public AdtA01Payload(AdtEventEntry entry) : base(entry)
    {
    }

    protected override void ApplyToEncounter(PatientEncounter patientEncounter)
    {
        patientEncounter.AdmitDate = AdmitDateTime ?? patientEncounter.AdmitDate;
    }
}
