using LantanaGroup.Link.Shared.Application.Models.Kafka;

namespace LantanaGroup.Link.Census.Application.Models.Payloads.Adt;

public static class AdtPayloadFactory
{
    /// <summary>
    /// Creates the payload for an ADT entry, or returns null for a message type Census does not track.
    /// </summary>
    public static AdtPayload? Create(AdtEventEntry entry)
    {
        return entry.MessageType?.Trim().ToUpperInvariant() switch
        {
            "A01" => new AdtA01Payload(entry),
            "A02" => new AdtA02Payload(entry),
            "A03" => new AdtA03Payload(entry),
            "A06" => new AdtA06Payload(entry),
            "A07" => new AdtA07Payload(entry),
            "A11" => new AdtA11Payload(entry),
            "A12" => new AdtA12Payload(entry),
            "A13" => new AdtA13Payload(entry),
            "A18" => new AdtA18Payload(entry),
            _ => null
        };
    }
}
