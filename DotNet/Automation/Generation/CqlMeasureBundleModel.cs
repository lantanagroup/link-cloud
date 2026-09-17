using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace LantanaGroup.Automation.Generation;

/// <summary>
/// Parsed view of one Measure FHIR bundle: CQL define bodies, code/valueset
/// declarations, ValueSet expansions, and the Measure's population/SDE roots.
/// </summary>
internal sealed class CqlMeasureBundleModel
{
    private static readonly Regex QuotedNamePattern = new("\"([^\"\\r\\n]+)\"", RegexOptions.Compiled);

    public IReadOnlyDictionary<string, string> Defines { get; }
    public IReadOnlyDictionary<string, string> CodeDeclarations { get; }
    public IReadOnlyDictionary<string, HashSet<string>> ValueSetCodes { get; }
    public IReadOnlySet<string> RootExpressionNames { get; }

    private CqlMeasureBundleModel(
        Dictionary<string, string> defines,
        Dictionary<string, string> codeDeclarations,
        Dictionary<string, HashSet<string>> valueSetCodes,
        HashSet<string> rootExpressionNames)
    {
        Defines = defines;
        CodeDeclarations = codeDeclarations;
        ValueSetCodes = valueSetCodes;
        RootExpressionNames = rootExpressionNames;
    }

    public static CqlMeasureBundleModel Parse(string bundleJson)
    {
        var defines = new Dictionary<string, string>(StringComparer.Ordinal);
        var codeDeclarations = new Dictionary<string, string>(StringComparer.Ordinal);
        var valuesetUrls = new Dictionary<string, string>(StringComparer.Ordinal);
        var roots = new HashSet<string>(StringComparer.Ordinal);
        var fhirValueSets = new List<FhirValueSet>();

        using var doc = JsonDocument.Parse(bundleJson);
        if (!doc.RootElement.TryGetProperty("entry", out var entries) || entries.ValueKind != JsonValueKind.Array)
            return new CqlMeasureBundleModel(defines, codeDeclarations, new(StringComparer.Ordinal), roots);

        foreach (var entry in entries.EnumerateArray())
        {
            if (!entry.TryGetProperty("resource", out var resource) || resource.ValueKind != JsonValueKind.Object)
                continue;

            var resourceType = resource.TryGetProperty("resourceType", out var rt) ? rt.GetString() : null;
            if (string.Equals(resourceType, "Library", StringComparison.Ordinal))
            {
                foreach (var cql in ExtractCqlTexts(resource))
                {
                    var stripped = CqlText.StripComments(cql);
                    foreach (var (name, body) in CqlText.ParseDefineBodies(stripped))
                    {
                        if (!defines.ContainsKey(name))
                            defines[name] = body;
                    }

                    foreach (var (name, code) in CqlText.ParseCodeDeclarations(stripped))
                        codeDeclarations[name] = code;

                    foreach (var (name, url) in CqlText.ParseValuesetDeclarations(stripped))
                        valuesetUrls[name] = url;
                }
            }
            else if (string.Equals(resourceType, "Measure", StringComparison.Ordinal))
            {
                AddCriteriaExpressions(resource, "group", "population", roots);
                AddCriteriaExpressions(resource, "supplementalData", null, roots);
            }
            else if (string.Equals(resourceType, "ValueSet", StringComparison.Ordinal))
            {
                fhirValueSets.Add(FhirValueSet.From(resource));
            }
        }

        var valueSetCodes = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var vs in fhirValueSets)
        {
            IndexValueSet(valueSetCodes, vs.Id, vs.Codes);
            IndexValueSet(valueSetCodes, vs.Name, vs.Codes);
            IndexValueSet(valueSetCodes, vs.Title, vs.Codes);
            IndexValueSet(valueSetCodes, vs.Url, vs.Codes);
        }

        foreach (var (cqlName, url) in valuesetUrls)
        {
            if (valueSetCodes.TryGetValue(NormalizeKey(url), out var byUrl)
                || valueSetCodes.TryGetValue(url, out byUrl))
            {
                IndexValueSet(valueSetCodes, cqlName, byUrl);
            }
        }

        return new CqlMeasureBundleModel(defines, codeDeclarations, valueSetCodes, roots);
    }

    public HashSet<string> ResolveReachableDefines()
    {
        var reachable = new HashSet<string>(StringComparer.Ordinal);
        var start = RootExpressionNames.Where(Defines.ContainsKey).ToHashSet(StringComparer.Ordinal);
        if (start.Count == 0)
            start = Defines.Keys.ToHashSet(StringComparer.Ordinal);

        var queue = new Queue<string>(start);
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (!reachable.Add(current))
                continue;
            if (!Defines.TryGetValue(current, out var body))
                continue;

            foreach (Match match in QuotedNamePattern.Matches(body))
            {
                var candidate = match.Groups[1].Value;
                if (Defines.ContainsKey(candidate) && !reachable.Contains(candidate))
                    queue.Enqueue(candidate);
            }
        }

        return reachable;
    }

    public HashSet<string>? CodesForValueSet(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return null;
        if (ValueSetCodes.TryGetValue(name, out var codes) && codes.Count > 0)
            return codes;
        var normalized = NormalizeKey(name);
        if (ValueSetCodes.TryGetValue(normalized, out codes) && codes.Count > 0)
            return codes;
        return null;
    }

    public string ResolveCode(string nameOrCode)
    {
        if (CodeDeclarations.TryGetValue(nameOrCode, out var declared))
            return declared;
        return nameOrCode;
    }

    private static void IndexValueSet(Dictionary<string, HashSet<string>> map, string? key, HashSet<string> codes)
    {
        if (string.IsNullOrWhiteSpace(key) || codes.Count == 0)
            return;
        foreach (var alias in new[] { key, NormalizeKey(key) }.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!map.TryGetValue(alias, out var existing))
            {
                map[alias] = new HashSet<string>(codes, StringComparer.OrdinalIgnoreCase);
                continue;
            }

            foreach (var code in codes)
                existing.Add(code);
        }
    }

    internal static string NormalizeKey(string value)
    {
        var normalized = value.Replace('\u00a0', ' ').Trim();
        return string.Join(' ', normalized.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }

    private static IEnumerable<string> ExtractCqlTexts(JsonElement library)
    {
        if (!library.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.Array)
            yield break;

        foreach (var item in content.EnumerateArray())
        {
            var contentType = item.TryGetProperty("contentType", out var ct) ? ct.GetString() : null;
            if (!string.Equals(contentType, "text/cql", StringComparison.OrdinalIgnoreCase))
                continue;
            var data = item.TryGetProperty("data", out var d) ? d.GetString() : null;
            if (string.IsNullOrEmpty(data))
                continue;
            string decoded;
            try
            {
                decoded = Encoding.UTF8.GetString(Convert.FromBase64String(data));
            }
            catch
            {
                continue;
            }

            yield return decoded;
        }
    }

    private static void AddCriteriaExpressions(
        JsonElement measure,
        string parentName,
        string? nestedName,
        HashSet<string> roots)
    {
        if (!measure.TryGetProperty(parentName, out var parent) || parent.ValueKind != JsonValueKind.Array)
            return;

        foreach (var item in parent.EnumerateArray())
        {
            if (nestedName != null)
            {
                if (!item.TryGetProperty(nestedName, out var nested) || nested.ValueKind != JsonValueKind.Array)
                    continue;
                foreach (var nestedItem in nested.EnumerateArray())
                    AddExpression(nestedItem, roots);
            }
            else
            {
                AddExpression(item, roots);
            }
        }
    }

    private static void AddExpression(JsonElement element, HashSet<string> roots)
    {
        if (!element.TryGetProperty("criteria", out var criteria) || criteria.ValueKind != JsonValueKind.Object)
            return;
        var expr = criteria.TryGetProperty("expression", out var exprProp) && exprProp.ValueKind == JsonValueKind.String
            ? exprProp.GetString()
            : null;
        if (!string.IsNullOrWhiteSpace(expr))
            roots.Add(expr);
    }

    private sealed record FhirValueSet(string? Id, string? Name, string? Title, string? Url, HashSet<string> Codes)
    {
        public static FhirValueSet From(JsonElement resource)
        {
            var codes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            CollectCodes(resource, codes);
            return new FhirValueSet(
                GetString(resource, "id"),
                GetString(resource, "name"),
                GetString(resource, "title"),
                GetString(resource, "url"),
                codes);
        }

        private static void CollectCodes(JsonElement resource, HashSet<string> codes)
        {
            if (resource.TryGetProperty("expansion", out var expansion)
                && expansion.TryGetProperty("contains", out var contains)
                && contains.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in contains.EnumerateArray())
                    AddCode(item, codes);
            }

            if (resource.TryGetProperty("compose", out var compose)
                && compose.TryGetProperty("include", out var include)
                && include.ValueKind == JsonValueKind.Array)
            {
                foreach (var inc in include.EnumerateArray())
                {
                    if (inc.TryGetProperty("concept", out var concepts) && concepts.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var concept in concepts.EnumerateArray())
                            AddCode(concept, codes);
                    }
                }
            }
        }

        private static void AddCode(JsonElement item, HashSet<string> codes)
        {
            var code = GetString(item, "code");
            if (!string.IsNullOrWhiteSpace(code))
                codes.Add(code);
        }

        private static string? GetString(JsonElement element, string name)
            => element.TryGetProperty(name, out var prop) && prop.ValueKind == JsonValueKind.String
                ? prop.GetString()
                : null;
    }
}
