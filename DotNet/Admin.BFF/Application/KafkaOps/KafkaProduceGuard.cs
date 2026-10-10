using System.Text;
using LantanaGroup.Link.Shared.Application.Models.Kafka;

namespace LantanaGroup.Link.LinkAdmin.BFF.Application.KafkaOps;

public sealed class KafkaProduceHeader
{
    public string Name { get; set; } = "";
    public string Value { get; set; } = "";
}

public sealed class KafkaProduceReview
{
    public bool Accepted { get; init; }
    public string Topic { get; init; } = "";
    public string Key { get; init; } = "";
    public string Value { get; init; } = "";
    public List<KafkaProduceHeader> Headers { get; init; } = [];
    public string Summary { get; init; } = "";
    public string Error { get; init; } = "";
}

/// <summary>
/// One message, checked before it is produced. Headers are lines of "name: value".
/// </summary>
public static class KafkaProduceGuard
{
    public const int MaxHeaders = 64;
    public const int MaxHeaderName = 200;
    public const int MaxHeaderValue = 8_192;
    public const int MaxKeyBytes = 8_192;
    public const int MaxValueBytes = 262_144;
    public const int MaxReason = 500;
    public const string ConfirmSentence = "Type the topic name to confirm. The message is produced only after the request.";
    public const string ReasonSentence = "A reason is required.";
    public const string ReasonLengthSentence = "The reason is too long.";

    public static KafkaProduceReview Evaluate(string? topic, string? headersText, string? key, string? value)
    {
        var admission = KafkaBrowseAllowList.Admit(topic, null);
        if (!admission.Allowed)
            return new KafkaProduceReview { Error = admission.Reason };

        var parsed = ParseHeaders(headersText);
        if (parsed.Error.Length > 0)
            return new KafkaProduceReview { Error = parsed.Error };

        var keyText = key ?? "";
        var valueText = value ?? "";
        if (Encoding.UTF8.GetByteCount(keyText) > MaxKeyBytes)
            return new KafkaProduceReview { Error = "The key is too long." };
        if (Encoding.UTF8.GetByteCount(valueText) > MaxValueBytes)
            return new KafkaProduceReview { Error = "The value is too long." };

        var headerWord = parsed.Headers.Count == 1 ? "header" : "headers";
        return new KafkaProduceReview
        {
            Accepted = true,
            Topic = admission.Topic,
            Key = keyText,
            Value = valueText,
            Headers = parsed.Headers,
            Summary = "One message on " + admission.Topic + ". " + parsed.Headers.Count + " " + headerWord + ". Key " + Encoding.UTF8.GetByteCount(keyText) + " bytes. Value " + Encoding.UTF8.GetByteCount(valueText) + " bytes."
        };
    }

    private static (List<KafkaProduceHeader> Headers, string Error) ParseHeaders(string? text)
    {
        var headers = new List<KafkaProduceHeader>();
        if (string.IsNullOrEmpty(text))
            return (headers, "");

        foreach (var raw in text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'))
        {
            if (string.IsNullOrWhiteSpace(raw))
                continue;
            if (headers.Count >= MaxHeaders)
                return (headers, "At most " + MaxHeaders + " headers can be produced.");

            var split = raw.IndexOf(':');
            if (split <= 0)
                return (headers, "Each header needs a name and a value, separated by a colon.");

            var name = raw[..split].Trim();
            var headerValue = raw[(split + 1)..];
            if (headerValue.StartsWith(' '))
                headerValue = headerValue[1..];
            if (name.Length == 0)
                return (headers, "Each header needs a name and a value, separated by a colon.");
            if (name.Length > MaxHeaderName)
                return (headers, "A header name is too long.");
            if (headerValue.Length > MaxHeaderValue)
                return (headers, "A header value is too long.");
            headers.Add(new KafkaProduceHeader { Name = name, Value = headerValue });
        }

        return (headers, "");
    }
}
