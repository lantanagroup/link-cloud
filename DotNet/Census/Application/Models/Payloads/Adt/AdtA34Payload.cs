using LantanaGroup.Link.Census.Application.Models.Enums;
using LantanaGroup.Link.Shared.Application.Models.Kafka;

namespace LantanaGroup.Link.Census.Application.Models.Payloads.Adt;

/// <summary>
/// Merge patient information - patient id only, an older form of A40. See <see cref="AdtPatientMergePayload"/>.
/// </summary>
public class AdtA34Payload : AdtPatientMergePayload
{
    public override EventType EventType => EventType.A34;

    public AdtA34Payload()
    {
    }

    public AdtA34Payload(AdtEventEntry entry) : base(entry)
    {
    }
}
