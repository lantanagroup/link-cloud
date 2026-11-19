using LantanaGroup.Link.Census.Application.Models.Enums;
using LantanaGroup.Link.Shared.Application.Models.Kafka;

namespace LantanaGroup.Link.Census.Application.Models.Payloads.Adt;

/// <summary>
/// Delete a patient record: the visit was entered in error. Some systems send this rather than an A11, so it is handled
/// as one.
/// </summary>
public class AdtA23Payload : AdtA11Payload
{
    public override EventType EventType => EventType.A23;

    public AdtA23Payload()
    {
    }

    public AdtA23Payload(AdtEventEntry entry) : base(entry)
    {
    }
}
