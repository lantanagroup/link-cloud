using LantanaGroup.Link.Census.Application.Models.Enums;
using LantanaGroup.Link.Shared.Application.Models.Kafka;

namespace LantanaGroup.Link.Census.Application.Models.Payloads.Adt;

/// <summary>
/// Register a patient, as for an emergency, observation or outpatient visit: handled as an A01. An A03 already admits
/// such a visit when it is the first message seen for it, so registering one opens it at the start instead.
/// </summary>
public class AdtA04Payload : AdtA01Payload
{
    public override EventType EventType => EventType.A04;

    public AdtA04Payload()
    {
    }

    public AdtA04Payload(AdtEventEntry entry) : base(entry)
    {
    }
}
