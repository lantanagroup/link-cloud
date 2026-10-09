using LantanaGroup.Link.Shared.Application.Models.Kafka;

namespace Link.UI.Services;

public readonly record struct KafkaRead(long Offset, int Count);

public static class KafkaMessageWindow
{
    public static int Size(int size) => size is 10 or 25 or 50 ? size : 25;

    public static int Pages(int matches, int size, long logEnd)
    {
        size = Size(size);
        if (matches > 0)
            return Math.Max(1, (int)Math.Ceiling(matches / (double)size));
        if (logEnd > 0)
            return Math.Max(1, (int)Math.Ceiling(logEnd / (double)size));
        return 1;
    }

    public static KafkaRead Locate(int page, int size, long logStart, long logEnd)
    {
        size = Size(size);
        if (page < 1)
            page = 1;
        if (logEnd < logStart)
            logEnd = logStart;
        var offset = logStart + ((long)page - 1) * size;
        if (logEnd > logStart && offset > logEnd)
            offset = logEnd;
        var room = logEnd > logStart
            ? (int)Math.Min(size, Math.Max(0, logEnd - offset))
            : size;
        return new KafkaRead(offset, room);
    }

    public static bool Matches(KafkaBrowseRecord record, string? text, string? key, string? header, string? value, string? kind, long? from, long? to)
    {
        if (!Contains(text, record))
            return false;
        if (!string.IsNullOrEmpty(key) && !string.Equals(key, "cap", StringComparison.Ordinal)
            && !(record.Key ?? "").Contains(key, StringComparison.OrdinalIgnoreCase))
            return false;
        if (!string.IsNullOrEmpty(header) && !record.Headers.Any(item =>
                item.Name.Contains(header, StringComparison.OrdinalIgnoreCase)
                || item.Value.Contains(header, StringComparison.OrdinalIgnoreCase)))
            return false;
        if (!string.IsNullOrEmpty(value)
            && !(record.Value ?? "").Contains(value, StringComparison.OrdinalIgnoreCase)
            && !(record.ValueSummary ?? "").Contains(value, StringComparison.OrdinalIgnoreCase))
            return false;
        if (!string.IsNullOrEmpty(kind))
        {
            var role = string.IsNullOrWhiteSpace(record.Link.ExceptionMessage) ? "Main" : "Error";
            var headers = string.Join('\n', record.Headers.Select(item => item.Name + " " + item.Value));
            if (role.IndexOf(kind, StringComparison.OrdinalIgnoreCase) < 0
                && headers.IndexOf(kind, StringComparison.OrdinalIgnoreCase) < 0)
                return false;
        }

        if (from is long start && record.TimestampUnixMs < start)
            return false;
        if (to is long end && record.TimestampUnixMs > end)
            return false;
        return true;
    }

    private static bool Contains(string? text, KafkaBrowseRecord record)
    {
        if (string.IsNullOrEmpty(text))
            return true;
        if ((record.Key ?? "").Contains(text, StringComparison.OrdinalIgnoreCase))
            return true;
        if ((record.Value ?? "").Contains(text, StringComparison.OrdinalIgnoreCase))
            return true;
        if ((record.ValueSummary ?? "").Contains(text, StringComparison.OrdinalIgnoreCase))
            return true;
        return record.Headers.Any(item =>
            item.Name.Contains(text, StringComparison.OrdinalIgnoreCase)
            || item.Value.Contains(text, StringComparison.OrdinalIgnoreCase));
    }
}
