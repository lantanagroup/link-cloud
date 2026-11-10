using LantanaGroup.Link.Census.Application.Models.Enums;
using LantanaGroup.Link.Shared.Application.Models.Kafka;

namespace LantanaGroup.Link.Census.Application.Models.Payloads.Adt;

/// <summary>
/// Change visit number: the patient's encounter with the visit number in MRG-5 is now known by the message's encounter
/// id. See <see cref="AdtVisitChangePayload"/>.
/// </summary>
public class AdtA50Payload : AdtVisitChangePayload
{
    public override EventType EventType => EventType.A50;

    public AdtA50Payload()
    {
    }

    public AdtA50Payload(AdtEventEntry entry) : base(entry)
    {
    }
}
