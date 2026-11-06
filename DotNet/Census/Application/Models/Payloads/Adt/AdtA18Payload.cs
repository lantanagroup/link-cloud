using LantanaGroup.Link.Census.Application.Models.Enums;
using LantanaGroup.Link.Shared.Application.Models.Kafka;

namespace LantanaGroup.Link.Census.Application.Models.Payloads.Adt;

/// <summary>
/// Merge patient information, the original merge event. See <see cref="AdtPatientMergePayload"/>.
/// </summary>
public class AdtA18Payload : AdtPatientMergePayload
{
    public override EventType EventType => EventType.A18;

    public AdtA18Payload()
    {
    }

    public AdtA18Payload(AdtEventEntry entry) : base(entry)
    {
    }
}
