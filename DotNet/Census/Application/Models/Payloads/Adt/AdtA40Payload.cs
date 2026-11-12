using LantanaGroup.Link.Census.Application.Models.Enums;
using LantanaGroup.Link.Shared.Application.Models.Kafka;

namespace LantanaGroup.Link.Census.Application.Models.Payloads.Adt;

/// <summary>
/// Merge patient - patient identifier list, which replaced A18. See <see cref="AdtPatientMergePayload"/>.
/// </summary>
public class AdtA40Payload : AdtPatientMergePayload
{
    public override EventType EventType => EventType.A40;

    public AdtA40Payload()
    {
    }

    public AdtA40Payload(AdtEventEntry entry) : base(entry)
    {
    }
}
