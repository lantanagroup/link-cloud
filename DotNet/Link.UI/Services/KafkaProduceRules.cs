using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using LantanaGroup.Link.Shared.Application.Models.Kafka;

namespace Link.UI.Services;

public sealed record KafkaProduceDraft
{
    public string Topic { get; init; } = "";
    public string Headers { get; init; } = "";
    public string Key { get; init; } = "";
    public string Value { get; init; } = "";
    public string Summary { get; init; } = "";
    public bool Ready { get; init; }
    public bool FromStage { get; init; }
}

public sealed class KafkaProduceReview
{
    public bool Accepted { get; init; }
    public string Topic { get; init; } = "";
    public string Key { get; init; } = "";
    public string Value { get; init; } = "";
    public List<KafkaBrowseHeader> Headers { get; init; } = [];
    public string Summary { get; init; } = "";
    public string Error { get; init; } = "";
}

/// <summary>
/// One message, checked before it is produced. The clipboard shape is
/// { headers: [{ name, value }], key, value }.
/// </summary>
public static class KafkaProduceRules
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
    public const string MissingSentence = "That message is not on this page. Fetch it, open it, then re-stage.";

    private static readonly JsonSerializerOptions ClipboardOptions = new()
    {
        Encoder = JavaScriptEncoder.Default
    };

    public static KafkaProduceDraft FromRecord(string topic, KafkaBrowseRecord record)
    {
        var headers = HeadersText(record.Headers);
        var review = Evaluate(topic, headers, record.Key, record.Value);
        return new KafkaProduceDraft
        {
            Topic = string.IsNullOrWhiteSpace(review.Topic) ? topic : review.Topic,
            Headers = headers,
            Key = record.Key ?? "",
            Value = record.Value ?? "",
            Summary = review.Accepted ? review.Summary : review.Error,
            Ready = review.Accepted,
            FromStage = true
        };
    }

    public static string HeadersText(IReadOnlyList<KafkaBrowseHeader> headers) =>
        string.Join("\n", headers.Select(header => header.Name + ": " + header.Value));

    public static string ClipboardJson(IReadOnlyList<KafkaBrowseHeader> headers, string? key, string? value) =>
        JsonSerializer.Serialize(new
        {
            headers = headers.Select(header => new { name = header.Name, value = header.Value }),
            key = key ?? "",
            value = value ?? ""
        }, ClipboardOptions);

    public static bool TryReadClipboard(string? json, out string headersText, out string key, out string value, out string error)
    {
        headersText = "";
        key = "";
        value = "";
        error = "";
        if (string.IsNullOrWhiteSpace(json))
        {
            error = "That clipboard is not a copied message.";
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                error = "That clipboard is not a copied message.";
                return false;
            }

            var root = document.RootElement;
            var headers = new List<KafkaBrowseHeader>();
            if (root.TryGetProperty("headers", out var headerNode))
            {
                if (headerNode.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in headerNode.EnumerateArray())
                    {
                        if (item.ValueKind != JsonValueKind.Object)
                            continue;
                        headers.Add(new KafkaBrowseHeader
                        {
                            Name = Text(item, "name"),
                            Value = Text(item, "value")
                        });
                    }
                }
                else if (headerNode.ValueKind == JsonValueKind.Object)
                {
                    foreach (var property in headerNode.EnumerateObject())
                    {
                        headers.Add(new KafkaBrowseHeader
                        {
                            Name = property.Name,
                            Value = property.Value.ValueKind == JsonValueKind.String ? property.Value.GetString() ?? "" : property.Value.ToString()
                        });
                    }
                }
            }

            key = root.TryGetProperty("key", out var keyNode) && keyNode.ValueKind == JsonValueKind.String ? keyNode.GetString() ?? "" : "";
            value = root.TryGetProperty("value", out var valueNode) && valueNode.ValueKind == JsonValueKind.String ? valueNode.GetString() ?? "" : "";
            if (!root.TryGetProperty("headers", out _) && !root.TryGetProperty("key", out _) && !root.TryGetProperty("value", out _))
            {
                error = "That clipboard is not a copied message.";
                return false;
            }

            headersText = HeadersText(headers);
            return true;
        }
        catch (JsonException)
        {
            error = "That clipboard is not a copied message.";
            return false;
        }
    }

    public static KafkaProduceReview Evaluate(string? topic, string? headersText, string? key, string? value)
    {
        var admission = KafkaBrowseAllowList.Admit(topic, null);
        if (!admission.Allowed)
            return Refuse(admission.Reason);

        var parsed = ParseHeaders(headersText);
        if (parsed.Error.Length > 0)
            return Refuse(parsed.Error);

        var keyText = key ?? "";
        var valueText = value ?? "";
        if (Encoding.UTF8.GetByteCount(keyText) > MaxKeyBytes)
            return Refuse("The key is too long.");
        if (Encoding.UTF8.GetByteCount(valueText) > MaxValueBytes)
            return Refuse("The value is too long.");

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

    private static (List<KafkaBrowseHeader> Headers, string Error) ParseHeaders(string? text)
    {
        var headers = new List<KafkaBrowseHeader>();
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
            headers.Add(new KafkaBrowseHeader { Name = name, Value = headerValue });
        }

        return (headers, "");
    }

    private static KafkaProduceReview Refuse(string error) =>
        new() { Error = error };

    private static string Text(JsonElement item, string name) =>
        item.TryGetProperty(name, out var node) && node.ValueKind == JsonValueKind.String ? node.GetString() ?? "" : "";
}
