using LantanaGroup.Link.Shared.Application.Models.Kafka;

namespace LantanaGroup.Link.Census.Application.Models.Payloads.Adt;

public static class AdtPayloadFactory
{
    private sealed record PayloadKind(Type Type, Func<AdtEventEntry, AdtPayload> Create);

    private static PayloadKind Kind<T>(Func<AdtEventEntry, T> create) where T : AdtPayload => new(typeof(T), create);

    /// <summary>
    /// The message types Census tracks, by trigger event, which is also each payload's stored PayloadType.
    /// </summary>
    private static readonly Dictionary<string, PayloadKind> Kinds = new(StringComparer.OrdinalIgnoreCase)
    {
        ["A01"] = Kind(e => new AdtA01Payload(e)),
        ["A03"] = Kind(e => new AdtA03Payload(e)),
        ["A04"] = Kind(e => new AdtA04Payload(e)),
        ["A06"] = Kind(e => new AdtA06Payload(e)),
        ["A07"] = Kind(e => new AdtA07Payload(e)),
        ["A08"] = Kind(e => new AdtA08Payload(e)),
        ["A11"] = Kind(e => new AdtA11Payload(e)),
        ["A13"] = Kind(e => new AdtA13Payload(e)),
        ["A18"] = Kind(e => new AdtA18Payload(e)),
        ["A23"] = Kind(e => new AdtA23Payload(e)),
        ["A30"] = Kind(e => new AdtA30Payload(e)),
        ["A34"] = Kind(e => new AdtA34Payload(e)),
        ["A36"] = Kind(e => new AdtA36Payload(e)),
        ["A39"] = Kind(e => new AdtA39Payload(e)),
        ["A40"] = Kind(e => new AdtA40Payload(e)),
        ["A42"] = Kind(e => new AdtA42Payload(e)),
        ["A45"] = Kind(e => new AdtA45Payload(e)),
        ["A50"] = Kind(e => new AdtA50Payload(e)),
    };

    /// <summary>
    /// Creates the payload for an ADT entry, or returns null for a message type Census does not track.
    /// </summary>
    public static AdtPayload? Create(AdtEventEntry entry)
    {
        var messageType = entry.MessageType?.Trim();
        return messageType is not null && Kinds.TryGetValue(messageType, out var kind) ? kind.Create(entry) : null;
    }

    /// <summary>
    /// The payload class stored under a PayloadType, or null when it is not an ADT payload.
    /// </summary>
    public static Type? GetPayloadType(string? payloadType) =>
        payloadType is not null && Kinds.TryGetValue(payloadType, out var kind) ? kind.Type : null;
}
