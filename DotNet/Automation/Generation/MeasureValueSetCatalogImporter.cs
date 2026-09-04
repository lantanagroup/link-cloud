using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace LantanaGroup.Automation.Generation;

/// <summary>
/// Reads NHSN measure FHIR bundles and turns CQL-retrieved value-set expansions
/// into generation-catalog rows. Uses the same embedded packages MeasureEval uses.
/// </summary>
public static class MeasureValueSetCatalogImporter
{
    /// <summary>
    /// <c>[Observation: "Glucose tests"]</c>, <c>[Encounter: class in "NHSN Inpatient Encounter Class Codes"]</c>.
    /// Group 1 is the FHIR type; group 2 is the value-set name when present.
    /// </summary>
    private static readonly Regex RetrievePattern = new(
        """\[\s*([A-Za-z]+)\s*(?:\]|:\s*(?:[A-Za-z]+\s+in\s+)?"([^"]+)"\s*\])""",
        RegexOptions.Compiled);

    /// <summary>
    /// CQL membership tests such as <c>GetMedicationCode(...) in "Diabetes Medications"</c>.
    /// NHSN often retrieves <c>[MedicationRequest]</c> untyped and filters with <c>in "…"</c>.
    /// </summary>
    private static readonly Regex MembershipPattern = new(
        @"\bin\s+""([^""]+)""",
        RegexOptions.Compiled);

    private static readonly Regex NearbyResourceTypePattern = new(
        """\b(Observation|Condition|Procedure|MedicationRequest|MedicationAdministration|Medication|ServiceRequest|DiagnosticReport|Specimen)\b""",
        RegexOptions.Compiled);

    public sealed record ImportResult(
        IReadOnlyList<GenerationCatalogItem> Items,
        IReadOnlyList<string> DiabetesMedicationCodes);

    public static ImportResult ImportAllEmbeddedMeasures()
    {
        var items = new List<GenerationCatalogItem>();
        var diabetes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var measure in Enum.GetValues<ProfiledMeasureType>())
        {
            string json;
            try
            {
                json = ProfiledMeasureCatalog.ReadBundleJson(measure);
            }
            catch (FileNotFoundException)
            {
                continue;
            }

            var one = Import(json, measure.ToString());
            items.AddRange(one.Items);
            foreach (var code in one.DiabetesMedicationCodes)
                diabetes.Add(code);
        }

        return new ImportResult(GenerationCatalogSeed.Dedupe(items), diabetes.ToList());
    }

    public static ImportResult Import(string bundleJson, string sourceMeasure)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bundleJson);

        using var doc = JsonDocument.Parse(bundleJson);
        if (!doc.RootElement.TryGetProperty("entry", out var entries) || entries.ValueKind != JsonValueKind.Array)
            return new ImportResult([], []);

        var expansions = new Dictionary<string, ValueSetExpansion>(StringComparer.OrdinalIgnoreCase);
        var valuesetUrls = new Dictionary<string, string>(StringComparer.Ordinal);
        var cqlChunks = new List<string>();

        foreach (var entry in entries.EnumerateArray())
        {
            if (!entry.TryGetProperty("resource", out var resource) || resource.ValueKind != JsonValueKind.Object)
                continue;
            var type = resource.TryGetProperty("resourceType", out var rt) ? rt.GetString() : null;
            if (string.Equals(type, "ValueSet", StringComparison.Ordinal))
            {
                var expansion = ValueSetExpansion.From(resource);
                if (expansion == null)
                    continue;
                foreach (var key in expansion.Keys)
                    expansions[key] = expansion;
            }
            else if (string.Equals(type, "Library", StringComparison.Ordinal))
            {
                foreach (var cql in ExtractCql(resource))
                {
                    cqlChunks.Add(cql);
                    var stripped = CqlText.StripComments(cql);
                    foreach (var (name, url) in CqlText.ParseValuesetDeclarations(stripped))
                        valuesetUrls[name] = url;
                }
            }
        }

        var vsByRetrieve = new Dictionary<string, HashSet<GenerationCatalogKind>>(StringComparer.OrdinalIgnoreCase);
        foreach (var cql in cqlChunks)
        {
            var stripped = CqlText.StripComments(cql);
            foreach (Match match in RetrievePattern.Matches(stripped))
            {
                var vsName = match.Groups[2].Success ? match.Groups[2].Value : "";
                if (string.IsNullOrWhiteSpace(vsName))
                    continue;
                if (!TryMapKind(match.Groups[1].Value, out var kind))
                    continue;
                AddKind(vsByRetrieve, vsName, kind);
            }

            foreach (Match match in MembershipPattern.Matches(stripped))
            {
                var vsName = match.Groups[1].Value;
                if (string.IsNullOrWhiteSpace(vsName) || ShouldSkipValueSet(vsName, null))
                    continue;

                if (TryResolveExpansion(vsName, valuesetUrls, expansions, out var membershipExpansion))
                {
                    if (IsDiabetesMedicationSet(vsName, membershipExpansion.Url))
                    {
                        AddKind(vsByRetrieve, vsName, GenerationCatalogKind.Medication);
                        continue;
                    }

                    if (TryGuessKindFromMembers(membershipExpansion, out var guessed))
                    {
                        AddKind(vsByRetrieve, vsName, guessed);
                        continue;
                    }
                }

                foreach (var kind in KindsNear(stripped, match.Index))
                    AddKind(vsByRetrieve, vsName, kind);
            }
        }

        foreach (var expansion in DistinctExpansions(expansions))
        {
            if (!IsDiabetesMedicationSet(expansion.Name, expansion.Url)
                && !IsDiabetesMedicationSet(expansion.Title, expansion.Url))
            {
                continue;
            }

            AddKind(vsByRetrieve, expansion.Name ?? expansion.Title ?? expansion.Url ?? "Diabetes Medications",
                GenerationCatalogKind.Medication);
        }

        var items = new List<GenerationCatalogItem>();
        var diabetes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var now = DateTimeOffset.UtcNow;

        foreach (var (vsName, kinds) in vsByRetrieve)
        {
            if (!TryResolveExpansion(vsName, valuesetUrls, expansions, out var expansion))
                continue;
            if (ShouldSkipValueSet(vsName, expansion.Url))
                continue;

            var diabetesSet = IsDiabetesMedicationSet(vsName, expansion.Url)
                || IsDiabetesMedicationSet(expansion.Name, expansion.Url)
                || IsDiabetesMedicationSet(expansion.Title, expansion.Url);
            foreach (var kind in kinds)
            {
                foreach (var member in expansion.Members)
                {
                    if (string.IsNullOrWhiteSpace(member.Code))
                        continue;
                    var system = GenerationCatalogItem.GuessSystem(kind, member.System, member.Code);
                    var display = string.IsNullOrWhiteSpace(member.Display) ? member.Code : member.Display;
                    var incomplete = kind == GenerationCatalogKind.Observation
                        && string.IsNullOrWhiteSpace(GuessObservationUnit(member.Code));
                    items.Add(new GenerationCatalogItem
                    {
                        Kind = kind,
                        System = system,
                        Code = member.Code,
                        Display = display!,
                        Category = kind == GenerationCatalogKind.Observation ? "laboratory" : null,
                        Unit = kind == GenerationCatalogKind.Observation ? GuessObservationUnit(member.Code) : null,
                        IsLab = kind == GenerationCatalogKind.ServiceRequest
                            && string.Equals(system, GenerationCatalogItem.Loinc, StringComparison.OrdinalIgnoreCase),
                        Incomplete = incomplete,
                        SourceValueSet = expansion.Url ?? vsName,
                        SourceMeasure = sourceMeasure,
                        IsSeed = false,
                        UpdatedAt = now
                    });
                    if (diabetesSet && kind == GenerationCatalogKind.Medication)
                        diabetes.Add(member.Code);
                }
            }
        }

        return new ImportResult(GenerationCatalogSeed.Dedupe(items), diabetes.ToList());
    }

    private static bool TryMapKind(string resourceType, out GenerationCatalogKind kind)
    {
        kind = resourceType switch
        {
            "Observation" => GenerationCatalogKind.Observation,
            "Condition" => GenerationCatalogKind.Condition,
            "Procedure" => GenerationCatalogKind.Procedure,
            "MedicationRequest" or "MedicationAdministration" or "Medication" => GenerationCatalogKind.Medication,
            "ServiceRequest" or "DiagnosticReport" => GenerationCatalogKind.ServiceRequest,
            "Specimen" => GenerationCatalogKind.Specimen,
            _ => default
        };
        return resourceType is "Observation" or "Condition" or "Procedure"
            or "MedicationRequest" or "MedicationAdministration" or "Medication"
            or "ServiceRequest" or "DiagnosticReport" or "Specimen";
    }

    private static void AddKind(
        Dictionary<string, HashSet<GenerationCatalogKind>> map,
        string vsName,
        GenerationCatalogKind kind)
    {
        if (string.IsNullOrWhiteSpace(vsName) || ShouldSkipValueSet(vsName, null))
            return;
        if (!map.TryGetValue(vsName, out var kinds))
        {
            kinds = [];
            map[vsName] = kinds;
        }

        kinds.Add(kind);
    }

    private static HashSet<GenerationCatalogKind> KindsNear(string cql, int index)
    {
        var start = Math.Max(0, index - 500);
        var length = Math.Min(cql.Length - start, 1000);
        var window = cql.Substring(start, length);
        var kinds = new HashSet<GenerationCatalogKind>();
        foreach (Match match in NearbyResourceTypePattern.Matches(window))
        {
            if (TryMapKind(match.Groups[1].Value, out var kind))
                kinds.Add(kind);
        }

        return kinds;
    }

    private static bool TryGuessKindFromMembers(ValueSetExpansion expansion, out GenerationCatalogKind kind)
    {
        kind = default;
        var systems = expansion.Members
            .Select(m => m.System)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToList();
        if (systems.Count == 0)
            return false;
        if (systems.TrueForAll(s =>
                s!.Contains("rxnorm", StringComparison.OrdinalIgnoreCase)
                || s.Contains("/ndc", StringComparison.OrdinalIgnoreCase)))
        {
            kind = GenerationCatalogKind.Medication;
            return true;
        }

        if (systems.TrueForAll(s => s!.Contains("loinc", StringComparison.OrdinalIgnoreCase)))
        {
            kind = GenerationCatalogKind.Observation;
            return true;
        }

        return false;
    }

    private static IEnumerable<ValueSetExpansion> DistinctExpansions(
        IReadOnlyDictionary<string, ValueSetExpansion> expansions)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var expansion in expansions.Values)
        {
            var key = expansion.Url ?? expansion.Name ?? expansion.Title ?? "";
            if (!seen.Add(key))
                continue;
            yield return expansion;
        }
    }

    private static bool ShouldSkipValueSet(string? name, string? url)
    {
        var blob = $"{name} {url}";
        return ContainsAny(blob,
            "Encounter Class",
            "Encounter Inpatient",
            "Emergency Department Visit",
            "Observation Services",
            "Discharge",
            "Location");
    }

    private static bool ContainsAny(string blob, params string[] needles)
        => needles.Any(n => blob.Contains(n, StringComparison.OrdinalIgnoreCase));

    private static bool TryResolveExpansion(
        string vsName,
        IReadOnlyDictionary<string, string> urls,
        IReadOnlyDictionary<string, ValueSetExpansion> expansions,
        out ValueSetExpansion expansion)
    {
        if (expansions.TryGetValue(vsName, out expansion!))
            return true;
        if (urls.TryGetValue(vsName, out var url) && expansions.TryGetValue(url, out expansion!))
            return true;
        var normalized = vsName.Replace('\u00a0', ' ').Trim();
        if (expansions.TryGetValue(normalized, out expansion!))
            return true;
        if (urls.TryGetValue(normalized, out url) && expansions.TryGetValue(url, out expansion!))
            return true;
        expansion = null!;
        return false;
    }

    private static bool IsDiabetesMedicationSet(string? name, string? url)
    {
        if (!string.IsNullOrWhiteSpace(name)
            && (name.Contains("Diabetes Medication", StringComparison.OrdinalIgnoreCase)
                || name.Contains("Antidiabetic", StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        return url != null
            && (url.Contains("1.4.1190.58", StringComparison.Ordinal)
                || url.Contains("1046.58", StringComparison.OrdinalIgnoreCase));
    }

    private static string? GuessObservationUnit(string code)
    {
        foreach (var o in FhirGenerationCodes.Observations)
        {
            if (string.Equals(o.Code, code, StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(o.Unit))
            {
                return o.Unit;
            }
        }

        return null;
    }

    private static IEnumerable<string> ExtractCql(JsonElement library)
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

    private sealed class ValueSetExpansion
    {
        public string? Url { get; init; }
        public string? Name { get; init; }
        public string? Title { get; init; }
        public List<Member> Members { get; init; } = [];
        public IEnumerable<string> Keys
        {
            get
            {
                foreach (var raw in new[] { Name, Title, Url })
                {
                    if (string.IsNullOrWhiteSpace(raw))
                        continue;
                    yield return raw!;
                    var normalized = raw!.Replace('\u00a0', ' ').Trim();
                    if (!string.Equals(normalized, raw, StringComparison.Ordinal))
                        yield return normalized;
                }
            }
        }

        public sealed record Member(string? System, string Code, string? Display);

        public static ValueSetExpansion? From(JsonElement resource)
        {
            var members = new List<Member>();
            Collect(resource, members);
            if (members.Count == 0)
                return null;
            return new ValueSetExpansion
            {
                Url = Str(resource, "url"),
                Name = Str(resource, "name") ?? Str(resource, "title"),
                Title = Str(resource, "title"),
                Members = members
            };
        }

        private static void Collect(JsonElement resource, List<Member> members)
        {
            if (resource.TryGetProperty("expansion", out var expansion)
                && expansion.TryGetProperty("contains", out var contains)
                && contains.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in contains.EnumerateArray())
                    Add(item, members);
            }

            if (resource.TryGetProperty("compose", out var compose)
                && compose.TryGetProperty("include", out var include)
                && include.ValueKind == JsonValueKind.Array)
            {
                foreach (var inc in include.EnumerateArray())
                {
                    var system = Str(inc, "system");
                    if (inc.TryGetProperty("concept", out var concepts) && concepts.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var concept in concepts.EnumerateArray())
                            Add(concept, members, system);
                    }
                }
            }
        }

        private static void Add(JsonElement item, List<Member> members, string? fallbackSystem = null)
        {
            var code = Str(item, "code");
            if (string.IsNullOrWhiteSpace(code))
                return;
            members.Add(new Member(Str(item, "system") ?? fallbackSystem, code, Str(item, "display")));
        }

        private static string? Str(JsonElement el, string name)
            => el.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null;
    }
}
