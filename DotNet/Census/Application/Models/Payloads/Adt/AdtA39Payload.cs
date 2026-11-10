using LantanaGroup.Link.Census.Application.Models.Enums;
using LantanaGroup.Link.Shared.Application.Models.Kafka;

namespace LantanaGroup.Link.Census.Application.Models.Payloads.Adt;

/// <summary>
/// Merge person - patient id, an older form of A40. See <see cref="AdtPatientMergePayload"/>.
/// </summary>
public class AdtA39Payload : AdtPatientMergePayload
{
    public override EventType EventType => EventType.A39;

    public AdtA39Payload()
    {
    }

    public AdtA39Payload(AdtEventEntry entry) : base(entry)
    {
    }
}
