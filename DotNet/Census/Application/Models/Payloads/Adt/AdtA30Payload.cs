using LantanaGroup.Link.Census.Application.Models.Enums;
using LantanaGroup.Link.Shared.Application.Models.Kafka;

namespace LantanaGroup.Link.Census.Application.Models.Payloads.Adt;

/// <summary>
/// Merge person information, an older form of A40. See <see cref="AdtPatientMergePayload"/>.
/// </summary>
public class AdtA30Payload : AdtPatientMergePayload
{
    public override EventType EventType => EventType.A30;

    public AdtA30Payload()
    {
    }

    public AdtA30Payload(AdtEventEntry entry) : base(entry)
    {
    }
}
