using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Automation.UI.Services.Persistence;

/// <summary>
/// Splits a snapshot payload so each Mongo document stays under the Cosmos DB
/// 2 MB limit. Collections become one document per record when that is practical,
/// and smaller values are packed. A single opaque value is split on UTF-8
/// boundaries and reassembled. Nothing is truncated.
/// </summary>
public static class SnapshotPartitioner
{
    public const int HardCapBytes = 2_000_000;
    public const int MaxDocumentJsonBytes = 1_048_576;
    public const int DocumentEnvelopeBytes = 2_048;
    public const int MinPracticalItemBytes = 512;
    public const int MaxSmallIndividualItems = 500;

    public const string ShapeProperty = "__snapshotShape";
    public const string ShapeValue = "parts";

    /// <summary>
    /// UTF-8 size of <paramref name="value"/> inside a JSON string written by
    /// <see cref="JsonSerializer"/> with its default encoder. Cosmos measures
    /// the document as that JSON, so quotes and non-ASCII count as \u escapes.
    /// </summary>
    public static int EscapedContentBytes(string value)
    {
        var bytes = 0;
        for (var i = 0; i < value.Length; i++)
        {
            var c = value[i];
            switch (c)
            {
                case '"' or '&' or '\'' or '+' or '<' or '>' or '`':
                    bytes += 6;
                    break;
                case '\\' or '\b' or '\f' or '\n' or '\r' or '\t':
                    bytes += 2;
                    break;
                default:
                    if (c is < (char)0x20 or (char)0x7F)
                    {
                        bytes += 6;
                    }
                    else if (c < 0x80)
                    {
                        bytes += 1;
                    }
                    else if (char.IsHighSurrogate(c)
                             && i + 1 < value.Length
                             && char.IsLowSurrogate(value[i + 1]))
                    {
                        bytes += 12;
                        i++;
                    }
                    else
                    {
                        bytes += 6;
                    }

                    break;
            }
        }

        return bytes;
    }

    /// <summary>
    /// UTF-8 size of a Mongo document whose string payload is <paramref name="data"/>.
    /// Counts JSON escaping and a fixed envelope for ids and field names.
    /// </summary>
    public static int EstimateStoredDocumentBytes(string data)
        => DocumentEnvelopeBytes + 2 + EscapedContentBytes(data);

    public static void EnsureWithinHardCap(string data)
    {
        var estimate = EstimateStoredDocumentBytes(data);
        if (estimate > HardCapBytes)
        {
            throw new SnapshotDocumentTooLargeException(
                $"Snapshot document is estimated at {estimate} UTF-8 bytes, over the {HardCapBytes} byte Cosmos limit.");
        }
    }

    /// <summary>
    /// Stored size of one part, including the payload, path, and optional item key.
    /// </summary>
    public static int EstimatePieceBytes(string data, string path, string? itemKey)
    {
        var bytes = EstimateStoredDocumentBytes(data) + EscapedContentBytes(path);
        if (!string.IsNullOrEmpty(itemKey))
            bytes += EscapedContentBytes(itemKey);
        return bytes;
    }

    /// <summary>
    /// Item keys are optional metadata. Drop one that would push the part over the budget.
    /// The value itself stays in the part payload.
    /// </summary>
    public static string? FitItemKey(string? itemKey, string data, string path, int budget)
    {
        if (string.IsNullOrEmpty(itemKey))
            return null;

        return EstimatePieceBytes(data, path, itemKey) <= budget ? itemKey : null;
    }

    public static SnapshotPlan Plan(string json, int? maxDocumentBytes = null)
    {
        ArgumentNullException.ThrowIfNull(json);
        var budget = maxDocumentBytes ?? MaxDocumentJsonBytes;
        if (budget <= DocumentEnvelopeBytes + 2)
        {
            throw new SnapshotDocumentTooLargeException(
                "Document budget is smaller than the smallest snapshot document.");
        }

        if (EstimateStoredDocumentBytes(json) <= budget)
            return new SnapshotPlan.Inline(json);

        using var doc = JsonDocument.Parse(json);
        var pieces = new List<SnapshotPiece>();
        JsonNode? skeleton;
        if (doc.RootElement.ValueKind == JsonValueKind.Array)
            skeleton = ExplodeArray(doc.RootElement, "$", pieces, budget);
        else if (doc.RootElement.ValueKind == JsonValueKind.Object)
            skeleton = ExplodeObject(doc.RootElement, "$", pieces, budget);
        else
            return BytePlan(json, pieces, budget);

        if (pieces.Count == 0)
            return BytePlan(json, pieces, budget);

        var skeletonJson = skeleton?.ToJsonString() ?? "null";
        var header = BuildHeaderJson(new string('0', 32), StructuredMode, pieces.Count, skeletonJson);
        if (EstimateStoredDocumentBytes(header) > budget)
            return BytePlan(json, [], budget);

        return new SnapshotPlan.Partitioned(StructuredMode, skeletonJson, pieces);
    }

    public static string Reassemble(string mode, string? skeletonJson, IReadOnlyList<SnapshotPiece> pieces)
    {
        if (string.Equals(mode, BytesMode, StringComparison.Ordinal))
        {
            return string.Concat(pieces
                .OrderBy(p => p.Index)
                .ThenBy(p => p.Slice)
                .Select(p => p.Data));
        }

        var root = skeletonJson == null ? null : JsonNode.Parse(skeletonJson);
        var rebuilt = Apply(root, "$", new PieceIndex(pieces));
        return rebuilt?.ToJsonString() ?? "null";
    }

    /// <summary>
    /// Stored-byte estimates include the document envelope, so a tiny value still
    /// looks larger than <see cref="MinPracticalItemBytes"/>. Compare the payload.
    /// </summary>
    private static bool IsLargeEnoughToItemize(int storedDocumentBytes)
        => storedDocumentBytes >= DocumentEnvelopeBytes + 2 + MinPracticalItemBytes;

    public static string BuildHeaderJson(string generationId, string mode, int partCount, string? skeletonJson)
    {
        var sb = new StringBuilder(256 + (skeletonJson?.Length ?? 0));
        sb.Append("{\"");
        sb.Append(ShapeProperty);
        sb.Append("\":\"");
        sb.Append(ShapeValue);
        sb.Append("\",\"version\":1,\"generationId\":");
        sb.Append(JsonSerializer.Serialize(generationId));
        sb.Append(",\"mode\":");
        sb.Append(JsonSerializer.Serialize(mode));
        sb.Append(",\"partCount\":");
        sb.Append(partCount);
        if (skeletonJson != null)
        {
            sb.Append(",\"skeleton\":");
            sb.Append(skeletonJson);
        }

        sb.Append('}');
        return sb.ToString();
    }

    public const string StructuredMode = "structured";
    public const string BytesMode = "bytes";

    /// <summary>
    /// A reader may return a payload only when every piece belongs to the
    /// committed generation. A short set is incomplete, not a mix of generations.
    /// </summary>
    public static string? ReadCommitted(
        string? headerJson,
        IReadOnlyList<SnapshotPiece> piecesForGeneration)
    {
        if (!SnapshotPartitionHeader.TryRead(headerJson, out var header) || header == null)
            return headerJson;

        if (piecesForGeneration.Count != header.PartCount)
            return null;

        return Reassemble(header.Mode, header.SkeletonJson, piecesForGeneration);
    }

    public static bool ShouldTranslateStoredData(string? data)
    {
        if (string.IsNullOrWhiteSpace(data))
            return false;

        if (SnapshotPartitionHeader.TryRead(data, out _))
            return false;

        if (SnapshotPartitionHeader.IsExternalPointer(data))
            return true;

        return EstimateStoredDocumentBytes(data) > MaxDocumentJsonBytes;
    }

    private static SnapshotPlan BytePlan(string json, List<SnapshotPiece> pieces, int budget)
    {
        pieces.Clear();
        AddSlices(pieces, "$", 0, json, budget);
        return new SnapshotPlan.Partitioned(BytesMode, null, pieces);
    }

    private static JsonArray ExplodeArray(JsonElement element, string path, List<SnapshotPiece> pieces, int budget)
    {
        var elements = new List<(JsonElement El, string Raw, int Bytes)>();
        foreach (var el in element.EnumerateArray())
        {
            var raw = el.GetRawText();
            elements.Add((el, raw, EstimateStoredDocumentBytes(raw)));
        }

        var recordLike = elements.Count > 0 && elements.TrueForAll(e =>
            e.El.ValueKind is JsonValueKind.Object or JsonValueKind.Array);
        var itemize = recordLike
            && (elements.Count <= MaxSmallIndividualItems
                || elements.TrueForAll(e => IsLargeEnoughToItemize(e.Bytes)));

        if (!itemize)
        {
            PackArray(path, elements, pieces, budget);
            return [];
        }

        for (var i = 0; i < elements.Count; i++)
        {
            var item = elements[i];
            if (item.Bytes <= budget)
            {
                pieces.Add(Piece("element", path, i, 0, item.Raw, ItemKey(item.El), budget));
                continue;
            }

            var before = pieces.Count;
            string? skeleton = null;
            if (item.El.ValueKind == JsonValueKind.Array)
                skeleton = ExplodeArray(item.El, path + "/" + i, pieces, budget).ToJsonString();
            else if (item.El.ValueKind == JsonValueKind.Object)
                skeleton = ExplodeObject(item.El, path + "/" + i, pieces, budget).ToJsonString();

            if (skeleton != null && EstimateStoredDocumentBytes(skeleton) <= budget)
            {
                pieces.Add(Piece("element", path, i, 0, skeleton, ItemKey(item.El), budget));
                continue;
            }

            if (before < pieces.Count)
                pieces.RemoveRange(before, pieces.Count - before);

            AddSlices(pieces, path, i, item.Raw, budget);
        }

        return [];
    }

    private static void PackArray(
        string path,
        List<(JsonElement El, string Raw, int Bytes)> elements,
        List<SnapshotPiece> pieces,
        int budget)
    {
        var sb = new StringBuilder();
        var contentBytes = 0;
        var count = 0;
        var startIndex = 0;

        void Flush(int nextIndex)
        {
            if (count == 0)
                return;

            sb.Append(']');
            pieces.Add(new SnapshotPiece("pack", path, startIndex, 0, sb.ToString(), null));
            sb.Clear();
            contentBytes = 0;
            count = 0;
            startIndex = nextIndex;
        }

        for (var i = 0; i < elements.Count; i++)
        {
            var raw = elements[i].Raw;
            if (elements[i].Bytes > budget)
            {
                Flush(i + 1);
                AddSlices(pieces, path, i, raw, budget);
                startIndex = i + 1;
                continue;
            }

            if (count > 0
                && DocumentEnvelopeBytes + 2 + contentBytes + EscapedContentBytes("," + raw + "]") > budget)
            {
                Flush(i);
            }

            if (count == 0)
            {
                sb.Append('[');
                contentBytes = EscapedContentBytes("[");
                startIndex = i;
            }
            else
            {
                sb.Append(',');
                contentBytes += EscapedContentBytes(",");
            }

            sb.Append(raw);
            contentBytes += EscapedContentBytes(raw);
            count++;
        }

        Flush(elements.Count);
    }

    private static JsonObject ExplodeObject(JsonElement element, string path, List<SnapshotPiece> pieces, int budget)
    {
        var kept = new JsonObject();
        var movable = new List<(string Name, string Raw, int Bytes)>();
        foreach (var prop in element.EnumerateObject())
        {
            var raw = prop.Value.GetRawText();
            var bytes = EstimateStoredDocumentBytes(raw);
            if (prop.Value.ValueKind == JsonValueKind.Array && bytes > budget)
            {
                kept[prop.Name] = ExplodeArray(prop.Value, ChildPath(path, prop.Name), pieces, budget);
                continue;
            }

            if (prop.Value.ValueKind == JsonValueKind.Object && bytes > budget)
            {
                kept[prop.Name] = ExplodeObject(prop.Value, ChildPath(path, prop.Name), pieces, budget);
                continue;
            }

            if (bytes > budget)
            {
                AddSlices(pieces, ChildPath(path, prop.Name), 0, raw, budget);
                kept[prop.Name] = JsonValue.Create((string?)null);
                continue;
            }

            movable.Add((prop.Name, raw, bytes));
        }

        foreach (var item in movable)
            kept[item.Name] = ParseFragment(item.Raw);

        var skeletonJson = kept.ToJsonString();
        var header = BuildHeaderJson(new string('0', 32), StructuredMode, Math.Max(pieces.Count, 1), skeletonJson);
        if (EstimateStoredDocumentBytes(header) <= budget)
            return kept;

        foreach (var item in movable)
            kept.Remove(item.Name);

        WriteEntries(path, movable, pieces, budget, kept);
        return kept;
    }

    private static void WriteEntries(
        string path,
        List<(string Name, string Raw, int Bytes)> entries,
        List<SnapshotPiece> pieces,
        int budget,
        JsonObject kept)
    {
        var itemize = entries.Count <= MaxSmallIndividualItems
            || entries.TrueForAll(e => IsLargeEnoughToItemize(e.Bytes));
        if (itemize)
        {
            for (var i = 0; i < entries.Count; i++)
            {
                var entryJson = EntryJson(entries[i].Name, entries[i].Raw);
                if (EstimateStoredDocumentBytes(entryJson) <= budget)
                {
                    pieces.Add(Piece("entry", path, i, 0, entryJson, entries[i].Name, budget));
                }
                else
                {
                    AddSlices(pieces, ChildPath(path, entries[i].Name), 0, entries[i].Raw, budget);
                    kept[entries[i].Name] = JsonValue.Create((string?)null);
                }
            }

            return;
        }

        var sb = new StringBuilder();
        var contentBytes = 0;
        var count = 0;
        var startIndex = 0;

        void Flush(int nextIndex)
        {
            if (count == 0)
                return;

            sb.Append('}');
            pieces.Add(new SnapshotPiece("entry-pack", path, startIndex, 0, sb.ToString(), null));
            sb.Clear();
            contentBytes = 0;
            count = 0;
            startIndex = nextIndex;
        }

        for (var i = 0; i < entries.Count; i++)
        {
            var encodedName = JsonSerializer.Serialize(entries[i].Name);
            var member = encodedName + ":" + entries[i].Raw;
            if (EstimateStoredDocumentBytes("{" + member + "}") > budget)
            {
                Flush(i + 1);
                AddSlices(pieces, ChildPath(path, entries[i].Name), 0, entries[i].Raw, budget);
                kept[entries[i].Name] = JsonValue.Create((string?)null);
                startIndex = i + 1;
                continue;
            }

            if (count > 0
                && DocumentEnvelopeBytes + 2 + contentBytes + EscapedContentBytes("," + member + "}") > budget)
            {
                Flush(i);
            }

            if (count == 0)
            {
                sb.Append('{');
                contentBytes = EscapedContentBytes("{");
                startIndex = i;
            }
            else
            {
                sb.Append(',');
                contentBytes += EscapedContentBytes(",");
            }

            sb.Append(member);
            contentBytes += EscapedContentBytes(member);
            count++;
        }

        Flush(entries.Count);
    }

    private static string EntryJson(string name, string rawValue)
        => "[" + JsonSerializer.Serialize(name) + "," + rawValue + "]";

    private static void AddSlices(List<SnapshotPiece> pieces, string path, int index, string raw, int budget)
    {
        var slice = 0;
        foreach (var part in SliceToBudget(raw, budget))
        {
            EnsureWithinHardCap(part);
            if (EstimateStoredDocumentBytes(part) > budget)
            {
                throw new SnapshotDocumentTooLargeException(
                    "A snapshot value could not be split under the document budget.");
            }

            pieces.Add(new SnapshotPiece("slice", path, index, slice, part, null));
            slice++;
        }
    }

    internal static IEnumerable<string> SliceToBudget(string value, int budget)
    {
        var start = 0;
        var content = 0;
        var length = 0;
        foreach (var rune in value.EnumerateRunes())
        {
            var piece = rune.ToString();
            var added = EscapedContentBytes(piece);
            if (length > 0 && DocumentEnvelopeBytes + 2 + content + added > budget)
            {
                yield return value.Substring(start, length);
                start += length;
                length = 0;
                content = 0;
            }

            if (DocumentEnvelopeBytes + 2 + added > budget)
            {
                throw new SnapshotDocumentTooLargeException(
                    "Document budget cannot hold a single UTF-8 character.");
            }

            length += piece.Length;
            content += added;
        }

        if (length > 0 || value.Length == 0)
            yield return value.Substring(start, length);
    }

    private static JsonNode? Apply(JsonNode? node, string path, PieceIndex index)
    {
        var current = node;
        if (index.TryGetDirect(path, out var direct))
            current = Materialize(current, direct);

        if (current is JsonObject obj)
        {
            foreach (var prop in obj.Select(p => p.Key).ToList())
            {
                var childPath = ChildPath(path, prop);
                if (!index.HasAtOrUnder(childPath))
                    continue;

                var child = obj[prop];
                var replacement = Apply(child, childPath, index);
                // Apply mutates a skeleton node in place and returns it. Writing
                // that same instance back throws because it already has a parent.
                if (!ReferenceEquals(replacement, child))
                    obj[prop] = replacement;
            }
        }
        else if (current is JsonArray array)
        {
            for (var i = 0; i < array.Count; i++)
            {
                var childPath = path + "/" + i.ToString(System.Globalization.CultureInfo.InvariantCulture);
                if (!index.HasAtOrUnder(childPath))
                    continue;

                var child = array[i];
                var replacement = Apply(child, childPath, index);
                if (!ReferenceEquals(replacement, child))
                    array[i] = replacement;
            }
        }

        return current;
    }

    private static JsonNode? Materialize(JsonNode? node, List<SnapshotPiece> direct)
    {
        if (direct.All(p => p.Kind is "entry" or "entry-pack"))
        {
            var obj = node as JsonObject ?? [];
            foreach (var piece in direct.OrderBy(p => p.Index).ThenBy(p => p.Slice))
            {
                if (piece.Kind == "entry")
                {
                    var pair = JsonNode.Parse(piece.Data)!.AsArray();
                    var key = pair[0]!.GetValue<string>();
                    obj[key] = pair[1]?.DeepClone();
                }
                else
                {
                    var pack = JsonNode.Parse(piece.Data)!.AsObject();
                    foreach (var prop in pack.ToList())
                    {
                        obj[prop.Key] = prop.Value?.DeepClone();
                    }
                }
            }

            return obj;
        }

        // A sliced property is one JSON value. A sliced array element must stay
        // inside the array, including when the array has only one element.
        if (node is not JsonArray
            && direct.All(p => p.Kind == "slice")
            && direct.Select(p => p.Index).Distinct().Count() == 1)
        {
            var json = string.Concat(direct.OrderBy(p => p.Slice).Select(p => p.Data));
            return JsonNode.Parse(json);
        }

        var array = new JsonArray();
        foreach (var group in direct.GroupBy(p => p.Index).OrderBy(g => g.Key))
        {
            var items = group.OrderBy(p => p.Slice).ToList();
            if (items[0].Kind == "pack")
            {
                var packed = JsonNode.Parse(items[0].Data)!.AsArray();
                foreach (var el in packed.ToList())
                    array.Add(el?.DeepClone());
            }
            else if (items[0].Kind == "slice")
            {
                var json = string.Concat(items.Select(p => p.Data));
                array.Add(JsonNode.Parse(json));
            }
            else
            {
                array.Add(JsonNode.Parse(items[0].Data));
            }
        }

        return array;
    }

    private static JsonNode? ParseFragment(string raw)
        => JsonNode.Parse(raw);

    private static SnapshotPiece Piece(string kind, string path, int index, int slice, string data, string? itemKey, int budget)
        => new(kind, path, index, slice, data, FitItemKey(itemKey, data, path, budget));

    private static string? ItemKey(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
            return null;

        foreach (var name in new[] { "Id", "id", "PatientId", "patientId", "MeasureReportId", "measureReportId" })
        {
            if (!element.TryGetProperty(name, out var prop))
                continue;

            return prop.ValueKind switch
            {
                JsonValueKind.String => prop.GetString(),
                JsonValueKind.Number => prop.GetRawText(),
                _ => null
            };
        }

        return null;
    }

    private static string ChildPath(string path, string propertyName)
        => path + "/" + propertyName.Replace("~", "~0", StringComparison.Ordinal).Replace("/", "~1", StringComparison.Ordinal);
}

public abstract record SnapshotPlan
{
    public sealed record Inline(string Json) : SnapshotPlan;

    public sealed record Partitioned(string Mode, string? SkeletonJson, IReadOnlyList<SnapshotPiece> Pieces) : SnapshotPlan;
}

public sealed record SnapshotPiece(string Kind, string Path, int Index, int Slice, string Data, string? ItemKey);

public sealed record SnapshotPartitionHeader(string GenerationId, string Mode, int PartCount, string? SkeletonJson)
{
    /// <summary>
    /// One level past System.Text.Json's default of 64, for the object
    /// <see cref="SnapshotPartitioner.BuildHeaderJson"/> adds around the skeleton.
    /// </summary>
    public const int HeaderJsonMaxDepth = 65;

    public static bool TryRead(string? data, out SnapshotPartitionHeader? header)
    {
        header = null;
        if (string.IsNullOrWhiteSpace(data))
            return false;

        try
        {
            // The skeleton can already be at the default nesting limit. The header
            // wraps it in one more object, so this parse allows that extra level.
            using var doc = JsonDocument.Parse(data, new JsonDocumentOptions { MaxDepth = HeaderJsonMaxDepth });
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
                return false;

            if (!doc.RootElement.TryGetProperty(SnapshotPartitioner.ShapeProperty, out var shape)
                || shape.ValueKind != JsonValueKind.String
                || !string.Equals(shape.GetString(), SnapshotPartitioner.ShapeValue, StringComparison.Ordinal))
            {
                return false;
            }

            if (!doc.RootElement.TryGetProperty("generationId", out var generation)
                || generation.ValueKind != JsonValueKind.String
                || string.IsNullOrWhiteSpace(generation.GetString()))
            {
                return false;
            }

            if (!doc.RootElement.TryGetProperty("mode", out var mode)
                || mode.ValueKind != JsonValueKind.String)
            {
                return false;
            }

            if (!doc.RootElement.TryGetProperty("partCount", out var count)
                || !count.TryGetInt32(out var partCount)
                || partCount < 0)
            {
                return false;
            }

            string? skeleton = null;
            if (doc.RootElement.TryGetProperty("skeleton", out var skeletonElement))
                skeleton = skeletonElement.GetRawText();

            header = new SnapshotPartitionHeader(generation.GetString()!, mode.GetString()!, partCount, skeleton);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    public static bool IsExternalPointer(string? data)
    {
        if (string.IsNullOrWhiteSpace(data))
            return false;

        try
        {
            using var doc = JsonDocument.Parse(data);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
                return false;

            if (!doc.RootElement.TryGetProperty("__externalSnapshotPayloadPointer", out var pointer)
                || pointer.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            if (!pointer.TryGetProperty("kind", out var kind)
                || kind.ValueKind != JsonValueKind.String
                || !string.Equals(kind.GetString(), SnapshotPayloadPointer.KindValue, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return pointer.TryGetProperty("blob", out var blob)
                && blob.ValueKind == JsonValueKind.String
                && !string.IsNullOrWhiteSpace(blob.GetString());
        }
        catch (JsonException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }
}

/// <summary>
/// Pieces grouped by path, plus every ancestor path, so reassembly does not
/// scan the full piece list for each record.
/// </summary>
internal sealed class PieceIndex
{
    private readonly Dictionary<string, List<SnapshotPiece>> _byPath;
    private readonly HashSet<string> _covered;

    public PieceIndex(IReadOnlyList<SnapshotPiece> pieces)
    {
        _byPath = new Dictionary<string, List<SnapshotPiece>>(pieces.Count, StringComparer.Ordinal);
        _covered = new HashSet<string>(StringComparer.Ordinal);
        foreach (var piece in pieces)
        {
            if (!_byPath.TryGetValue(piece.Path, out var list))
            {
                list = new List<SnapshotPiece>();
                _byPath[piece.Path] = list;
            }

            list.Add(piece);
            var path = piece.Path;
            while (_covered.Add(path))
            {
                var slash = path.LastIndexOf('/');
                if (slash <= 0)
                    break;

                path = path.Substring(0, slash);
            }
        }
    }

    public bool TryGetDirect(string path, out List<SnapshotPiece> pieces)
        => _byPath.TryGetValue(path, out pieces!);

    public bool HasAtOrUnder(string path) => _covered.Contains(path);
}

public sealed class SnapshotDocumentTooLargeException : Exception
{
    public SnapshotDocumentTooLargeException(string message)
        : base(message)
    {
    }
}
