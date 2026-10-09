using System.Text.Json;

namespace LantanaGroup.Link.Shared.Application.Models.Kafka;

public readonly record struct KafkaCorrelationHit(string Topic, int Partition, long Offset);

/// <summary>
/// Decides whether a browse stopped because of a real cap. Reaching the end of a
/// partition, or timing out after the high watermark was already read, is not a cap.
/// </summary>
public static class KafkaBrowseStop
{
    public static bool IsCap(bool hitBytes, bool hitScan, bool reachedEnd, bool timedOut, long nextOffset, long high)
    {
        if (hitBytes || hitScan)
            return true;
        if (reachedEnd || nextOffset >= high)
            return false;
        return timedOut;
    }
}

public static class KafkaBrowseTarget
{
    public static bool TryPartition(string? key, int partitionCount, out int partition)
    {
        partition = 0;
        if (partitionCount < 1 || !LinkMessageKey.TryDeserialize(key, out var parsed) || parsed is null)
            return false;
        partition = KafkaMurmur.Partition(parsed.Serialize(), partitionCount);
        return true;
    }
}

public readonly record struct KafkaBrowseCursor(long Start, long? Stop);

public static class KafkaBrowseResume
{
    public static Dictionary<int, KafkaBrowseCursor> Parse(string? text)
    {
        var map = new Dictionary<int, KafkaBrowseCursor>();
        if (string.IsNullOrWhiteSpace(text))
            return map;
        foreach (var piece in text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var split = piece.Split(':');
            if (split.Length is < 2 or > 3)
                continue;
            if (!int.TryParse(split[0], out var partition) || partition < 0)
                continue;
            if (!long.TryParse(split[1], out var offset) || offset < 0)
                continue;
            long? stop = null;
            if (split.Length == 3 && long.TryParse(split[2], out var end) && end > offset)
                stop = end;
            map[partition] = new KafkaBrowseCursor(offset, stop);
            if (map.Count >= KafkaBrowseLimits.MaxPartitions)
                break;
        }

        return map;
    }

    public static string Format(IEnumerable<KeyValuePair<int, KafkaBrowseCursor>> points) =>
        string.Join(",", points.OrderBy(point => point.Key).Select(point =>
            point.Value.Stop is long stop
                ? point.Key + ":" + point.Value.Start + ":" + stop
                : point.Key + ":" + point.Value.Start));
}

public static class KafkaCorrelationLog
{
    public static bool TryHit(string? line, string? correlationId, out KafkaCorrelationHit hit)
    {
        hit = default;
        if (string.IsNullOrWhiteSpace(line) || string.IsNullOrWhiteSpace(correlationId))
            return false;
        if (line.IndexOf(correlationId, StringComparison.OrdinalIgnoreCase) < 0)
            return false;
        if (line.TrimStart().StartsWith('{') && TryJson(line, out hit))
            return true;
        return TryText(line, out hit);
    }

    private static bool TryJson(string line, out KafkaCorrelationHit hit)
    {
        hit = default;
        try
        {
            using var document = JsonDocument.Parse(line);
            var root = document.RootElement;
            var topic = Text(root, "topic") ?? Text(root, "Topic");
            var partition = Whole(root, "partition") ?? Whole(root, "Partition");
            var offset = Whole(root, "offset") ?? Whole(root, "Offset");
            if (string.IsNullOrWhiteSpace(topic) || partition is null || offset is null || partition < 0 || offset < 0)
                return false;
            hit = new KafkaCorrelationHit(topic, (int)partition.Value, offset.Value);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool TryText(string line, out KafkaCorrelationHit hit)
    {
        hit = default;
        var topic = After(line, "topic");
        var partition = After(line, "partition");
        var offset = After(line, "offset");
        if (topic.Length == 0 || !int.TryParse(partition, out var partitionId) || !long.TryParse(offset, out var offsetId))
            return false;
        if (partitionId < 0 || offsetId < 0)
            return false;
        hit = new KafkaCorrelationHit(topic, partitionId, offsetId);
        return true;
    }

    private static string After(string line, string label)
    {
        var index = line.IndexOf(label, StringComparison.OrdinalIgnoreCase);
        if (index < 0)
            return "";
        index += label.Length;
        while (index < line.Length && (line[index] is ' ' or ':' or '=' or '"'))
            index++;
        var end = index;
        while (end < line.Length && line[end] is not (' ' or ',' or '"' or '}' or '\n' or '\r'))
            end++;
        return end > index ? line[index..end] : "";
    }

    private static string? Text(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var value))
            return null;
        return value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    }

    private static long? Whole(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var value))
            return null;
        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var number))
            return number;
        if (value.ValueKind == JsonValueKind.String && long.TryParse(value.GetString(), out var parsed))
            return parsed;
        return null;
    }
}
