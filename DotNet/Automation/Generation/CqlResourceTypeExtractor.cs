using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace LantanaGroup.Automation.Generation;

/// <summary>
/// Extracts the set of FHIR resource types that a measure's CQL actually retrieves.
/// This is the authoritative source for "what resource types will MeasureEval's CQL
/// engine look for in the patient bundle?" — and therefore what types will end up as
/// contained resources in the MeasureReport and ultimately in ABS patient artifacts.
///
/// CQL retrieve expressions have the form <c>[ResourceType]</c> or
/// <c>[ResourceType: "ValueSetName"]</c>. We extract the resource type token from
/// every Library resource's <c>text/cql</c> content attachment.
/// </summary>
public static class CqlResourceTypeExtractor
{
    /// <summary>
    /// Matches CQL retrieve expressions: <c>[Encounter]</c>, <c>[Encounter: "..."]</c>,
    /// <c>[Encounter: class in "..."]</c>, etc.
    /// Captures the resource type name in group 1.
    /// </summary>
    private static readonly Regex RetrievePattern = new(
        @"\[\s*([A-Z][a-zA-Z]+)\s*[\]:]",
        RegexOptions.Compiled);

    /// <summary>
    /// Extracts the distinct set of FHIR resource types retrieved by CQL in the given
    /// measure bundle JSON. Scans all Library resources for <c>text/cql</c> content
    /// and collects every <c>[ResourceType]</c> or <c>[ResourceType: ...]</c> expression.
    /// </summary>
    public static HashSet<string> ExtractFromBundleJson(string bundleJson)
    {
        var types = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        using var doc = JsonDocument.Parse(bundleJson);
        if (!doc.RootElement.TryGetProperty("entry", out var entries) || entries.ValueKind != JsonValueKind.Array)
            return types;

        foreach (var entry in entries.EnumerateArray())
        {
            if (!entry.TryGetProperty("resource", out var resource))
                continue;

            var resourceType = resource.TryGetProperty("resourceType", out var rtProp) ? rtProp.GetString() : null;
            if (!string.Equals(resourceType, "Library", StringComparison.Ordinal))
                continue;

            if (!resource.TryGetProperty("content", out var contentArray) || contentArray.ValueKind != JsonValueKind.Array)
                continue;

            foreach (var content in contentArray.EnumerateArray())
            {
                var contentType = content.TryGetProperty("contentType", out var ctProp) ? ctProp.GetString() : null;
                if (!string.Equals(contentType, "text/cql", StringComparison.OrdinalIgnoreCase))
                    continue;

                var data = content.TryGetProperty("data", out var dataProp) ? dataProp.GetString() : null;
                if (string.IsNullOrEmpty(data))
                    continue;

                string cql;
                try
                {
                    cql = Encoding.UTF8.GetString(Convert.FromBase64String(data));
                }
                catch
                {
                    continue;
                }

                foreach (Match match in RetrievePattern.Matches(cql))
                {
                    var typeName = match.Groups[1].Value;
                    // Filter out CQL built-in types that look like resource names
                    if (!IsBuiltInCqlType(typeName))
                        types.Add(typeName);
                }
            }
        }

        return types;
    }

    /// <summary>
    /// Extracts resource types from retrieve expressions that are reachable from Measure
    /// population criteria define expressions.
    /// </summary>
    public static HashSet<string> ExtractReachableFromBundleJson(string bundleJson)
    {
        var roots = ExtractMeasurePopulationExpressionNames(bundleJson);
        var cqlTexts = ExtractCqlTextsFromBundle(bundleJson);

        if (cqlTexts.Count == 0)
            return [];

        var reachableTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var cql in cqlTexts)
        {
            var defineBodies = ParseDefineBodies(cql);
            if (defineBodies.Count == 0)
            {
                foreach (Match match in RetrievePattern.Matches(cql))
                {
                    var typeName = match.Groups[1].Value;
                    if (!IsBuiltInCqlType(typeName))
                        reachableTypes.Add(typeName);
                }

                continue;
            }

            var startNodes = roots.Where(defineBodies.ContainsKey).ToHashSet(StringComparer.Ordinal);
            if (startNodes.Count == 0)
            {
                // If we cannot resolve roots, fall back to all define bodies.
                startNodes = defineBodies.Keys.ToHashSet(StringComparer.Ordinal);
            }

            var reachableDefines = ResolveReachableDefines(defineBodies, startNodes);
            foreach (var defineName in reachableDefines)
            {
                var body = defineBodies[defineName];
                foreach (Match match in RetrievePattern.Matches(body))
                {
                    var typeName = match.Groups[1].Value;
                    if (!IsBuiltInCqlType(typeName))
                        reachableTypes.Add(typeName);
                }
            }
        }

        return reachableTypes;
    }

    /// <summary>
    /// Union of reachable CQL retrieve types across inline measure-bundle JSON payloads.
    /// </summary>
    public static HashSet<string> ExtractReachableFromBundleJsons(IEnumerable<string> bundleJsons)
    {
        var union = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var json in bundleJsons)
        {
            if (string.IsNullOrWhiteSpace(json))
                continue;

            foreach (var t in ExtractReachableFromBundleJson(json))
                union.Add(t);
        }

        return union;
    }

    private static HashSet<string> ExtractMeasurePopulationExpressionNames(string bundleJson)
    {
        var expressions = new HashSet<string>(StringComparer.Ordinal);

        using var doc = JsonDocument.Parse(bundleJson);
        if (!doc.RootElement.TryGetProperty("entry", out var entries) || entries.ValueKind != JsonValueKind.Array)
            return expressions;

        foreach (var entry in entries.EnumerateArray())
        {
            if (!entry.TryGetProperty("resource", out var resource) || resource.ValueKind != JsonValueKind.Object)
                continue;

            var resourceType = resource.TryGetProperty("resourceType", out var rtProp) ? rtProp.GetString() : null;
            if (!string.Equals(resourceType, "Measure", StringComparison.Ordinal))
                continue;

            // Population criteria expressions (group[].population[].criteria.expression)
            if (resource.TryGetProperty("group", out var groups) && groups.ValueKind == JsonValueKind.Array)
            {
                foreach (var group in groups.EnumerateArray())
                {
                    if (!group.TryGetProperty("population", out var populations) || populations.ValueKind != JsonValueKind.Array)
                        continue;

                    foreach (var pop in populations.EnumerateArray())
                    {
                        AddCriteriaExpression(pop, expressions);
                    }
                }
            }

            // Supplemental Data expressions (supplementalData[].criteria.expression)
            // SDE expressions are critical — most resource types are only retrieved via
            // SDE defines (e.g., "SDE Condition", "SDE Coverage"), not via the population
            // criteria. Without these roots, the reachability walk misses nearly all types.
            if (resource.TryGetProperty("supplementalData", out var sdArray) && sdArray.ValueKind == JsonValueKind.Array)
            {
                foreach (var sd in sdArray.EnumerateArray())
                {
                    AddCriteriaExpression(sd, expressions);
                }
            }
        }

        return expressions;
    }

    private static void AddCriteriaExpression(JsonElement element, HashSet<string> expressions)
    {
        if (!element.TryGetProperty("criteria", out var criteria) || criteria.ValueKind != JsonValueKind.Object)
            return;

        var expr = criteria.TryGetProperty("expression", out var exprProp) && exprProp.ValueKind == JsonValueKind.String
            ? exprProp.GetString()
            : null;

        if (!string.IsNullOrWhiteSpace(expr))
            expressions.Add(expr);
    }

    private static List<string> ExtractCqlTextsFromBundle(string bundleJson)
    {
        var cqlTexts = new List<string>();

        using var doc = JsonDocument.Parse(bundleJson);
        if (!doc.RootElement.TryGetProperty("entry", out var entries) || entries.ValueKind != JsonValueKind.Array)
            return cqlTexts;

        foreach (var entry in entries.EnumerateArray())
        {
            if (!entry.TryGetProperty("resource", out var resource))
                continue;

            var resourceType = resource.TryGetProperty("resourceType", out var rtProp) ? rtProp.GetString() : null;
            if (!string.Equals(resourceType, "Library", StringComparison.Ordinal))
                continue;

            if (!resource.TryGetProperty("content", out var contentArray) || contentArray.ValueKind != JsonValueKind.Array)
                continue;

            foreach (var content in contentArray.EnumerateArray())
            {
                var contentType = content.TryGetProperty("contentType", out var ctProp) ? ctProp.GetString() : null;
                if (!string.Equals(contentType, "text/cql", StringComparison.OrdinalIgnoreCase))
                    continue;

                var data = content.TryGetProperty("data", out var dataProp) ? dataProp.GetString() : null;
                if (string.IsNullOrEmpty(data))
                    continue;

                try
                {
                    cqlTexts.Add(Encoding.UTF8.GetString(Convert.FromBase64String(data)));
                }
                catch
                {
                    // ignore malformed content
                }
            }
        }

        return cqlTexts;
    }

    private static Dictionary<string, string> ParseDefineBodies(string cql)
    {
        var defines = new Dictionary<string, string>(StringComparer.Ordinal);
        var definePattern = new Regex(
            "(?im)^\\s*define\\s+(?:\"([^\"\\r\\n]+)\"|([A-Za-z_][A-Za-z0-9_]*))\\s*:",
            RegexOptions.Compiled);

        var matches = definePattern.Matches(cql);
        for (var i = 0; i < matches.Count; i++)
        {
            var current = matches[i];
            var name = current.Groups[1].Success ? current.Groups[1].Value : current.Groups[2].Value;
            if (string.IsNullOrWhiteSpace(name))
                continue;

            var bodyStart = current.Index + current.Length;
            var bodyEnd = i + 1 < matches.Count ? matches[i + 1].Index : cql.Length;
            if (bodyEnd <= bodyStart)
                continue;

            defines[name] = cql[bodyStart..bodyEnd];
        }

        return defines;
    }

    private static HashSet<string> ResolveReachableDefines(
        Dictionary<string, string> defineBodies,
        HashSet<string> roots)
    {
        var reachable = new HashSet<string>(StringComparer.Ordinal);
        var queue = new Queue<string>(roots);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (!reachable.Add(current))
                continue;

            if (!defineBodies.TryGetValue(current, out var body))
                continue;

            var quotedCallPattern = new Regex("\"([^\"\\r\\n]+)\"", RegexOptions.Compiled);
            foreach (Match match in quotedCallPattern.Matches(body))
            {
                var candidate = match.Groups[1].Value;
                if (defineBodies.ContainsKey(candidate) && !reachable.Contains(candidate))
                    queue.Enqueue(candidate);
            }
        }

        return reachable;
    }

    private static bool IsBuiltInCqlType(string name) => name switch
    {
        // CQL built-in types that syntactically match the [Type] pattern
        "Interval" or "List" or "Tuple" or "Code" or "Concept" or "Quantity" => true,
        _ => false
    };
}
