using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Link.UI.Services;

/// <summary>
/// A patient's resources as type counts plus a paged identifier list.
/// The summary never returns every resource. One raw body is loaded only when a node is opened.
/// </summary>
public static class ResourceGraphRules
{
    public const int MaxEntries = 20_000;
    public const int ProgressEvery = 2_000;
    public const int DefaultPageSize = 25;
    public const int MaxPageSize = 50;
    public const int MaxRawChars = 16_000;
    public const int MaxBundleChars = 32_000_000;
    public const int MaxStoredBodyChars = 4_000_000;
    public const int MaxRefsOnNode = 8;
    public const int ScalePatientCount = 5_000;
    public const string ScalePatientId = "44444444-4444-4444-4444-444444444444";

    public static readonly IReadOnlyList<(string Type, int Count)> ScaleMix =
    [
        ("Observation", 9_000),
        ("Condition", 2_100),
        ("MedicationRequest", 1_400),
        ("Procedure", 800),
        ("DiagnosticReport", 600),
        ("ServiceRequest", 400),
        ("Encounter", 350),
        ("Specimen", 200),
        ("AllergyIntolerance", 120),
        ("Patient", 30)
    ];

    private static readonly JsonSerializerOptions Camel = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static readonly byte[] Newline = [(byte)'\n'];
    private static readonly object CacheGate = new();
    private static string? CachedKey;
    private static ResourceGraphIndex? CachedIndex;
    private static long CachedAt;

    public static int ScaleMixTotal
    {
        get
        {
            var total = 0;
            foreach (var (_, count) in ScaleMix)
                total += count;
            return total;
        }
    }

    public static bool TryFixture(string? patientId, bool scale, out GraphSpec spec)
    {
        spec = null!;
        var id = (patientId ?? string.Empty).Trim();
        if (id.Length is 0 or > 80)
            return false;

        if (scale)
        {
            if (string.Equals(id, ScalePatientId, StringComparison.OrdinalIgnoreCase))
            {
                spec = new GraphSpec(ScalePatientId, ScaleMix);
                return true;
            }

            if (!TryScaleIndex(id, out var index))
                return false;

            var total = ScalePatientTotal(index);
            spec = new GraphSpec(ScaleId(index), [("Observation", total)]);
            return true;
        }

        if (!TrySampleIndex(id, out var sample))
            return false;

        var observation = 41 - sample;
        spec = new GraphSpec(SampleId(sample), [("Observation", observation), ("Encounter", 1)]);
        return true;
    }

    public static ResourceGraphIndex Build(GraphSpec spec, CancellationToken cancellationToken, int cap = MaxEntries) =>
        Collect(EnumerateSynthetic(spec, cancellationToken), spec.PatientId, synthetic: true, Math.Clamp(cap, 1, MaxEntries), cancellationToken);

    public static ResourceGraphIndex ReadBundle(string? json, string patientId, CancellationToken cancellationToken, int cap = MaxEntries)
    {
        cancellationToken.ThrowIfCancellationRequested();
        cap = Math.Clamp(cap, 1, MaxEntries);
        var started = Stopwatch.GetTimestamp();
        if (string.IsNullOrWhiteSpace(json))
            return Finish(patientId, [], null, false, false, false, "The bundle was empty.", started);

        if (json.Length > MaxBundleChars)
            return Finish(patientId, [], null, false, true, false, "The bundle is larger than 32 MB, so the graph was not built.", started);

        var nodes = new List<GraphNode>(256);
        Dictionary<string, string>? slices = [];
        var stored = 0;
        var truncated = false;
        var dropped = false;
        string? error = null;
        try
        {
            var bytes = Encoding.UTF8.GetBytes(json);
            var reader = new Utf8JsonReader(bytes, new JsonReaderOptions { CommentHandling = JsonCommentHandling.Skip, MaxDepth = 64 });
            Walk(reader, bytes, patientId, cap, nodes, slices, ref stored, ref truncated, ref dropped, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (JsonException)
        {
            error = "The bundle could not be read.";
            nodes.Clear();
            slices = null;
        }

        return Finish(patientId, nodes, slices, dropped, truncated, false, error, started);
    }

    public static async Task SummarizeAsync(ResourceGraphIndex index, Stream stream, CancellationToken cancellationToken)
    {
        var marks = index.ProgressMarks;
        for (var mark = ProgressEvery; mark < index.Total; mark += ProgressEvery)
        {
            if (!marks.Contains(mark))
                continue;
            await WriteLineAsync(stream, new { kind = "progress", read = mark, cap = MaxEntries }, cancellationToken);
        }

        await WriteLineAsync(stream, new
        {
            kind = "done",
            patientId = index.PatientId,
            total = index.Total,
            truncated = index.Truncated,
            elapsedMs = index.ElapsedMs,
            error = index.Error,
            types = index.Types.Select(type => new { name = type.Name, count = type.Count })
        }, cancellationToken);
    }

    public static void Remember(string key, ResourceGraphIndex index)
    {
        lock (CacheGate)
        {
            CachedKey = key;
            CachedIndex = index;
            CachedAt = Environment.TickCount64;
        }
    }

    public static ResourceGraphIndex? Recall(string key)
    {
        lock (CacheGate)
        {
            if (!string.Equals(CachedKey, key, StringComparison.Ordinal) || CachedIndex is null)
                return null;
            if (Environment.TickCount64 - CachedAt > 120_000)
                return null;
            return CachedIndex;
        }
    }

    public static string SyntheticId(int number) =>
        "22222222-2222-4222-8222-" + number.ToString("x12", CultureInfo.InvariantCulture);

    public static int ScaleResourceTotal()
    {
        var total = ScaleMixTotal;
        for (var index = 2; index <= ScalePatientCount; index++)
            total += ScalePatientTotal(index);
        return total;
    }

    public static int ScalePatientTotal(int index) => index <= 1 ? ScaleMixTotal : Math.Max(1, 500 - index);

    public static string ScaleId(int index) => index == 1
        ? ScalePatientId
        : "patient-" + index.ToString("00000", CultureInfo.InvariantCulture);

    private static async Task WriteLineAsync<T>(Stream stream, T value, CancellationToken cancellationToken)
    {
        await JsonSerializer.SerializeAsync(stream, value, Camel, cancellationToken);
        await stream.WriteAsync(Newline, cancellationToken);
        await stream.FlushAsync(cancellationToken);
    }

    private static ResourceGraphIndex Collect(
        IEnumerable<GraphNode> nodes,
        string patientId,
        bool synthetic,
        int cap,
        CancellationToken cancellationToken)
    {
        var started = Stopwatch.GetTimestamp();
        var list = new List<GraphNode>(Math.Min(cap, 256));
        var truncated = false;
        foreach (var node in nodes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (list.Count >= cap)
            {
                truncated = true;
                break;
            }

            list.Add(node);
        }

        return Finish(patientId, list, null, false, truncated, synthetic, null, started);
    }

    private static IEnumerable<GraphNode> EnumerateSynthetic(GraphSpec spec, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var firstEncounter = -1;
        var cursor = 0;
        foreach (var (type, count) in spec.Counts)
        {
            if (count <= 0)
                continue;
            if (string.Equals(type, "Encounter", StringComparison.Ordinal))
            {
                firstEncounter = cursor;
                break;
            }

            cursor += count;
        }

        var number = 0;
        foreach (var (type, count) in spec.Counts)
        {
            if (count <= 0 || string.IsNullOrWhiteSpace(type))
                continue;

            for (var offset = 0; offset < count; offset++)
            {
                if ((number & 1023) == 0)
                    cancellationToken.ThrowIfCancellationRequested();
                var id = SyntheticId(number);
                var refs = RefsFor(spec.PatientId, type, firstEncounter);
                yield return new GraphNode(type.Trim(), id, refs);
                number++;
            }
        }
    }

    private static string[] RefsFor(string patientId, string type, int firstEncounter)
    {
        var patient = "Patient/" + patientId;
        if (string.Equals(type, "Patient", StringComparison.Ordinal))
            return [patient];
        if (string.Equals(type, "Encounter", StringComparison.Ordinal) || firstEncounter < 0)
            return [patient];
        if (string.Equals(type, "Observation", StringComparison.Ordinal))
            return [patient, "Encounter/" + SyntheticId(firstEncounter)];
        return [patient];
    }

    private static void Walk(
        Utf8JsonReader reader,
        byte[] bytes,
        string patientId,
        int cap,
        List<GraphNode> nodes,
        Dictionary<string, string> slices,
        ref int stored,
        ref bool truncated,
        ref bool dropped,
        CancellationToken cancellationToken)
    {
        var depth = 0;
        var inResource = false;
        var resourceDepth = -1;
        var expectResource = false;
        var resourceStart = 0L;
        string? property = null;
        string? type = null;
        string? id = null;
        var refs = new List<string>(4);
        var nextIsType = false;
        var nextIsId = false;

        while (reader.Read())
        {
            switch (reader.TokenType)
            {
                case JsonTokenType.StartObject:
                    depth++;
                    if (expectResource && !inResource)
                    {
                        inResource = true;
                        resourceDepth = depth;
                        resourceStart = reader.TokenStartIndex;
                        type = null;
                        id = null;
                        refs.Clear();
                    }

                    expectResource = false;
                    break;

                case JsonTokenType.EndObject:
                    if (inResource && depth == resourceDepth)
                    {
                        var isCenter = string.Equals(type, "Patient", StringComparison.Ordinal)
                            && string.Equals(id, patientId, StringComparison.Ordinal);
                        if (!isCenter && !string.IsNullOrEmpty(type) && !string.IsNullOrEmpty(id))
                        {
                            if (nodes.Count >= cap)
                            {
                                truncated = true;
                                return;
                            }

                            var kept = refs.Distinct(StringComparer.Ordinal).Take(MaxRefsOnNode).ToArray();
                            var node = new GraphNode(type, id, kept);
                            nodes.Add(node);
                            StoreSlice(bytes, resourceStart, reader.BytesConsumed, node, slices, ref stored, ref dropped);
                            if ((nodes.Count & 1023) == 0)
                                cancellationToken.ThrowIfCancellationRequested();
                        }

                        inResource = false;
                    }

                    depth--;
                    expectResource = false;
                    nextIsType = false;
                    nextIsId = false;
                    break;

                case JsonTokenType.PropertyName:
                    property = reader.GetString();
                    expectResource = !inResource && string.Equals(property, "resource", StringComparison.Ordinal);
                    nextIsType = inResource && depth == resourceDepth && string.Equals(property, "resourceType", StringComparison.Ordinal);
                    nextIsId = inResource && depth == resourceDepth && string.Equals(property, "id", StringComparison.Ordinal);
                    break;

                case JsonTokenType.String:
                    if (nextIsType)
                        type = reader.GetString();
                    else if (nextIsId)
                        id = reader.GetString();
                    else if (inResource && string.Equals(property, "reference", StringComparison.Ordinal))
                    {
                        var normalized = NormalizeRef(reader.GetString());
                        if (normalized is not null)
                            refs.Add(normalized);
                    }

                    nextIsType = false;
                    nextIsId = false;
                    expectResource = false;
                    break;

                default:
                    nextIsType = false;
                    nextIsId = false;
                    if (reader.TokenType is not JsonTokenType.StartObject and not JsonTokenType.StartArray)
                        expectResource = false;
                    break;
            }
        }
    }

    private static void StoreSlice(
        byte[] bytes,
        long start,
        long consumed,
        GraphNode node,
        Dictionary<string, string> slices,
        ref int stored,
        ref bool dropped)
    {
        if (dropped || start < 0 || consumed <= start || consumed > bytes.Length)
            return;

        var length = (int)(consumed - start);
        if (stored + length > MaxStoredBodyChars)
        {
            dropped = true;
            slices.Clear();
            stored = 0;
            return;
        }

        slices[node.Type + "/" + node.Id] = Encoding.UTF8.GetString(bytes, (int)start, length);
        stored += length;
    }

    private static string? NormalizeRef(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var text = value.Trim();
        if (text.StartsWith('#'))
            return null;

        var slash = text.LastIndexOf('/');
        if (slash <= 0 || slash >= text.Length - 1)
            return null;

        var typeStart = text.LastIndexOf('/', slash - 1);
        var type = text[(typeStart + 1)..slash];
        var id = text[(slash + 1)..];
        if (type.Length is 0 or > 64 || id.Length is 0 or > 80 || type.Contains(':'))
            return null;

        return type + "/" + id;
    }

    private static ResourceGraphIndex Finish(
        string patientId,
        List<GraphNode> nodes,
        Dictionary<string, string>? slices,
        bool dropped,
        bool truncated,
        bool synthetic,
        string? error,
        long started)
    {
        var elapsed = (int)Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        var marks = new HashSet<int>();
        for (var mark = ProgressEvery; mark < nodes.Count; mark += ProgressEvery)
            marks.Add(mark);

        return new ResourceGraphIndex(patientId, nodes, slices, dropped, truncated, synthetic, error, elapsed, marks);
    }

    private static bool TrySampleIndex(string id, out int index)
    {
        index = 0;
        if (string.Equals(id, "11111111-1111-1111-1111-111111111112", StringComparison.OrdinalIgnoreCase))
        {
            index = 1;
            return true;
        }

        if (id.Length == 10
            && id.StartsWith("patient-", StringComparison.OrdinalIgnoreCase)
            && int.TryParse(id[8..], NumberStyles.None, CultureInfo.InvariantCulture, out index)
            && index is >= 2 and <= 40)
            return true;

        index = 0;
        return false;
    }

    private static string SampleId(int index) => index == 1
        ? "11111111-1111-1111-1111-111111111112"
        : "patient-" + index.ToString("00", CultureInfo.InvariantCulture);

    private static bool TryScaleIndex(string id, out int index)
    {
        index = 0;
        if (id.Length == 13
            && id.StartsWith("patient-", StringComparison.OrdinalIgnoreCase)
            && int.TryParse(id[8..], NumberStyles.None, CultureInfo.InvariantCulture, out index)
            && index is >= 2 and <= ScalePatientCount)
            return true;

        index = 0;
        return false;
    }
}

public sealed class GraphSpec
{
    public GraphSpec(string patientId, IReadOnlyList<(string Type, int Count)> counts)
    {
        PatientId = patientId;
        Counts = counts;
    }

    public string PatientId { get; }
    public IReadOnlyList<(string Type, int Count)> Counts { get; }
}

public sealed class ResourceGraphIndex
{
    private readonly List<GraphNode> _nodes;
    private readonly Dictionary<string, string>? _slices;
    private readonly bool _synthetic;

    internal ResourceGraphIndex(
        string patientId,
        List<GraphNode> nodes,
        Dictionary<string, string>? slices,
        bool bodiesDropped,
        bool truncated,
        bool synthetic,
        string? error,
        int elapsedMs,
        HashSet<int> progressMarks)
    {
        PatientId = patientId;
        _nodes = nodes;
        _slices = slices;
        BodiesDropped = bodiesDropped;
        Truncated = truncated;
        _synthetic = synthetic;
        Error = error;
        ElapsedMs = elapsedMs;
        ProgressMarks = progressMarks;
        Types = nodes
            .GroupBy(node => node.Type, StringComparer.Ordinal)
            .Select(group => new ResourceTypeCount(group.Key, group.Count()))
            .OrderByDescending(type => type.Count)
            .ThenBy(type => type.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public string PatientId { get; }
    public int Total => _nodes.Count;
    public bool Truncated { get; }
    public bool BodiesDropped { get; }
    public string? Error { get; }
    public int ElapsedMs { get; }
    public IReadOnlyList<ResourceTypeCount> Types { get; }
    internal HashSet<int> ProgressMarks { get; }

    public ResourceGraphPage Page(string? type, string? query, int page, int pageSize)
    {
        var started = Stopwatch.GetTimestamp();
        var size = pageSize <= 0 ? ResourceGraphRules.DefaultPageSize : Math.Min(pageSize, ResourceGraphRules.MaxPageSize);
        var wanted = (type ?? string.Empty).Trim();
        var text = (query ?? string.Empty).Trim();
        var requested = Math.Max(page, 1);
        var window = TakePage(wanted, text, (requested - 1) * size, size, out var total);
        var pages = total == 0 ? 0 : (int)Math.Ceiling(total / (double)size);
        var current = pages == 0 ? 1 : Math.Min(requested, pages);
        if (current != requested)
            window = TakePage(wanted, text, (current - 1) * size, size, out _);
        var taken = window.Select(node => new ResourceGraphRecord(node.Type, node.Id, node.Refs)).ToList();
        var elapsed = (int)Math.Max(0, Stopwatch.GetElapsedTime(started).TotalMilliseconds);
        return new ResourceGraphPage(wanted, taken, new ResourceGraphMetadata(size, current, total, pages), elapsed);
    }

    private List<GraphNode> TakePage(string type, string text, int skip, int size, out int total)
    {
        total = 0;
        var window = new List<GraphNode>(size);
        foreach (var node in _nodes)
        {
            if (type.Length > 0 && !string.Equals(node.Type, type, StringComparison.OrdinalIgnoreCase))
                continue;
            if (text.Length > 0 && !Matches(node, text))
                continue;
            if (total >= skip && window.Count < size)
                window.Add(node);
            total++;
        }

        return window;
    }

    public ResourceGraphRaw? Raw(string? type, string? id)
    {
        var wantedType = (type ?? string.Empty).Trim();
        var wantedId = (id ?? string.Empty).Trim();
        GraphNode? found = null;
        foreach (var node in _nodes)
        {
            if (string.Equals(node.Type, wantedType, StringComparison.OrdinalIgnoreCase)
                && string.Equals(node.Id, wantedId, StringComparison.Ordinal))
            {
                found = node;
                break;
            }
        }

        if (found is null)
            return null;

        if (_slices is not null && _slices.TryGetValue(found.Type + "/" + found.Id, out var slice))
        {
            var pretty = Pretty(slice, out var truncated);
            return new ResourceGraphRaw(found.Type, found.Id, found.Refs, pretty, truncated, null);
        }

        if (_synthetic)
            return new ResourceGraphRaw(found.Type, found.Id, found.Refs, SyntheticJson(found), false, null);

        var note = BodiesDropped
            ? "This bundle is too large to keep each resource. Download the measure eval input for the full JSON."
            : "This resource body was not kept.";
        return new ResourceGraphRaw(found.Type, found.Id, found.Refs, null, false, note);
    }

    private static bool Matches(GraphNode node, string text) =>
        node.Id.Contains(text, StringComparison.OrdinalIgnoreCase)
        || node.Type.Contains(text, StringComparison.OrdinalIgnoreCase)
        || node.Refs.Any(item => item.Contains(text, StringComparison.OrdinalIgnoreCase));

    private string SyntheticJson(GraphNode node)
    {
        var subject = "Patient/" + PatientId;
        var payload = new Dictionary<string, object?>
        {
            ["resourceType"] = node.Type,
            ["id"] = node.Id,
            ["status"] = "final",
            ["subject"] = new Dictionary<string, string> { ["reference"] = subject }
        };
        var encounter = node.Refs.FirstOrDefault(item => item.StartsWith("Encounter/", StringComparison.Ordinal));
        if (encounter is not null)
            payload["encounter"] = new Dictionary<string, string> { ["reference"] = encounter };
        return JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
    }

    private static string? Pretty(string json, out bool truncated)
    {
        truncated = false;
        try
        {
            using var document = JsonDocument.Parse(json);
            var pretty = JsonSerializer.Serialize(document.RootElement, new JsonSerializerOptions { WriteIndented = true });
            if (pretty.Length <= ResourceGraphRules.MaxRawChars)
                return pretty;
            truncated = true;
            return pretty[..ResourceGraphRules.MaxRawChars];
        }
        catch (JsonException)
        {
            if (json.Length <= ResourceGraphRules.MaxRawChars)
                return json;
            truncated = true;
            return json[..ResourceGraphRules.MaxRawChars];
        }
    }
}

public sealed record ResourceTypeCount(string Name, int Count);

public sealed record ResourceGraphRecord(string Type, string Id, IReadOnlyList<string> Refs);

public sealed record ResourceGraphMetadata(int PageSize, int PageNumber, int TotalCount, int TotalPages);

public sealed record ResourceGraphPage(
    string Type,
    IReadOnlyList<ResourceGraphRecord> Records,
    ResourceGraphMetadata Metadata,
    int ElapsedMs);

public sealed record ResourceGraphRaw(
    string Type,
    string Id,
    IReadOnlyList<string> Refs,
    string? Json,
    bool Truncated,
    string? Note);

internal sealed class GraphNode
{
    public GraphNode(string type, string id, string[] refs)
    {
        Type = type;
        Id = id;
        Refs = refs;
    }

    public string Type { get; }
    public string Id { get; }
    public string[] Refs { get; }
}
