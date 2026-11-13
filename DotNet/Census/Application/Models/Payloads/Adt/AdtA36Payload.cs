using LantanaGroup.Link.Census.Application.Models.Enums;
using LantanaGroup.Link.Shared.Application.Models.Kafka;

namespace LantanaGroup.Link.Census.Application.Models.Payloads.Adt;

/// <summary>
/// Merge patient information - patient id and account number, an older form of A40. The account number part is not applied. See <see cref="AdtPatientMergePayload"/>.
/// </summary>
public class AdtA36Payload : AdtPatientMergePayload
{
    public override EventType EventType => EventType.A36;

    public AdtA36Payload()
    {
    }

    public AdtA36Payload(AdtEventEntry entry) : base(entry)
    {
    }
}
