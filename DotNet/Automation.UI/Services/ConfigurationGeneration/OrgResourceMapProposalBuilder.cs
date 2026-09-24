using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Automation.UI.Models;

namespace Automation.UI.Services.ConfigurationGeneration;

public static class OrgResourceMapProposalBuilder
{
    private static readonly Regex IdentifierExists = new(
        @"^(?:Location\.)?identifier\.(?:exists|where)\(\s*system\s*=\s*'((?:\\.|[^'\\])+)'(?:\s+and\s+value\s*=\s*'((?:\\.|[^'\\])+)')?\s*\)(?:\.exists\(\s*\))?$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex TypeExists = new(
        @"^(?:Location\.)?type\.coding\.(?:exists|where)\(\s*system\s*=\s*'((?:\\.|[^'\\])+)'(?:\s+and\s+code\s*=\s*'((?:\\.|[^'\\])+)')?\s*\)(?:\.exists\(\s*\))?(?:\s+and\s+Location\.alias\s*=\s*'((?:\\.|[^'\\])*)')?$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static GeneratedOrmProposal Build(
        BundleConfigFingerprint fingerprint,
        IReadOnlyList<OrganizationResourceMapTemplate> existing,
        OrganizationResourceMapTemplate? refineExisting = null)
    {
        var proposal = new GeneratedOrmProposal
        {
            SuggestedName = refineExisting is { IsSystem: false }
                ? refineExisting.Name
                : "",
            SuggestedDescription = "Auto-built so every Location can pass org mapping during acquisition (any-match), using identifiers or type codes already on the raw resource — not codes cleanup adds later."
        };

        var conditions = new List<OrganizationResourceMapCondition>();
        var priority = 1;

        if (refineExisting != null)
        {
            foreach (var condition in refineExisting.Conditions)
            {
                if (string.IsNullOrWhiteSpace(condition.FhirPath))
                    continue;
                conditions.Add(new OrganizationResourceMapCondition
                {
                    FhirPath = condition.FhirPath.Trim(),
                    Priority = priority++
                });
            }
        }

        foreach (var system in DistinctIdentifierSystems(fingerprint))
        {
            var path = $"Location.identifier.where(system = '{Escape(system)}').exists()";
            if (conditions.Any(c => CoversIdentifierSystem(c.FhirPath, system)))
                continue;
            conditions.Add(new OrganizationResourceMapCondition { FhirPath = path, Priority = priority++ });
        }

        // Type conditions are a fallback for Locations that already have type codes on the
        // raw upload. Acquisition evaluates org mapping before Copy Location / CodeMap, so
        // never propose type matches that cleanup would invent later.
        var needTypeFallback = fingerprint.LocationsWithoutIdentifier > 0 || DistinctIdentifierSystems(fingerprint).Count == 0;
        if (needTypeFallback)
        {
            foreach (var type in fingerprint.LocationTypes.Where(t => !string.IsNullOrWhiteSpace(t.System)))
            {
                var path = string.IsNullOrWhiteSpace(type.Code)
                    ? $"Location.type.coding.where(system = '{Escape(type.System)}').exists()"
                    : $"Location.type.coding.where(system = '{Escape(type.System)}' and code = '{Escape(type.Code)}').exists()";
                if (conditions.Any(c => CoversType(c.FhirPath, type.System, type.Code)))
                    continue;
                conditions.Add(new OrganizationResourceMapCondition { FhirPath = path, Priority = priority++ });
            }
        }

        proposal.Conditions = conditions;

        if (fingerprint.LocationCount == 0)
            proposal.Notes.Add("No Location resources were found. An ORM still needs at least one Location match to be useful.");
        else if (conditions.Count == 0)
            proposal.Notes.Add("Locations were present but had no identifier system or type coding that can become a match row.");
        else
            proposal.Notes.Add($"Proposed {conditions.Count} match condition(s) against the raw Location shape acquisition sees. Any matching condition lets a Location pass.");

        proposal.Notes.Add("Org mapping runs during acquisition, before cleanup copies identifiers or aliases onto Location.type. Maps must match identifiers (or type codes already on the upload), not codes Copy Location / CodeMap add later.");

        if (needTypeFallback && fingerprint.LocationTypes.Count > 0)
            proposal.Notes.Add("Type-coding conditions were added only for type codes already present on the uploaded Locations.");

        if (fingerprint.LocationsWithoutIdentifier > 0)
            proposal.Notes.Add($"{fingerprint.LocationsWithoutIdentifier} Location(s) had no identifier.");

        proposal.Reuse = ScoreExisting(fingerprint, existing);
        return proposal;
    }

    public static List<ReuseCandidate> ScoreExisting(
        BundleConfigFingerprint fingerprint,
        IReadOnlyList<OrganizationResourceMapTemplate> existing)
    {
        var neededIdentifiers = NeededIdentifierKeys(fingerprint);
        var rawTypeKeys = NeededRawTypeKeys(fingerprint);
        var rawLocations = fingerprint.RawLocations;
        var results = new List<ReuseCandidate>();

        foreach (var template in existing)
        {
            var covered = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var codeKeys = new HashSet<string>(TypeCodeKeyComparer.Instance);
            var valueKeys = new HashSet<string>(TypeCodeKeyComparer.Instance);
            var aliasKeys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var condition in template.Conditions)
            {
                foreach (var key in ParseKeys(condition.FhirPath))
                {
                    if (IsAliasKey(key))
                        aliasKeys.Add(key);
                    else if (key.StartsWith("type|", StringComparison.OrdinalIgnoreCase))
                        codeKeys.Add(key);
                    else if (key.StartsWith("id|", StringComparison.OrdinalIgnoreCase))
                        valueKeys.Add(key);
                    else
                        covered.Add(key);
                }
            }

            var hasIdentifier = valueKeys.Count > 0 || covered.Any(IsIdentifierKey);
            var hasType = covered.Any(IsTypeKey) || codeKeys.Count > 0 || aliasKeys.Count > 0;

            ReuseCandidate? identifierCandidate = null;
            if (hasIdentifier && neededIdentifiers.Count > 0)
            {
                var mapIdentifierKeys = covered.Where(IsIdentifierKey).Concat(valueKeys).ToList();
                var identifierSatisfied = mapIdentifierKeys.Count(key => IdentifierKeySatisfied(key, neededIdentifiers));
                if (identifierSatisfied > 0)
                {
                    var hit = neededIdentifiers.Count(neededKey => IsCovered(neededKey, covered, valueKeys));
                    var score = (double)hit / neededIdentifiers.Count;
                    var siblingValues = UncoveredIdentifiersAreSiblingValues(neededIdentifiers, covered, valueKeys);
                    identifierCandidate = ToCandidate(
                        template,
                        score,
                        score >= 0.999
                            ? "This map already matches the Location identifiers acquisition will see."
                            : template.IsSystem
                                ? $"This system map matches {hit} of {neededIdentifiers.Count} Location identifiers from the upload. Extending clones a custom copy so the system map stays unchanged."
                                : $"This map matches {hit} of {neededIdentifiers.Count} Location identifiers from the upload.",
                        reuse: siblingValues);
                    // Conditions are alternatives. A partial identifier match can still be
                    // Ready to use when a type condition on the same map matches.
                    if (identifierCandidate.Recommendation == "Reuse" || !hasType)
                    {
                        results.Add(identifierCandidate);
                        continue;
                    }
                }
            }

            if (!hasType)
                continue;

            void AddBest(ReuseCandidate? typeCandidate)
            {
                if (typeCandidate?.Recommendation == "Reuse")
                    results.Add(typeCandidate);
                else if (identifierCandidate != null)
                    results.Add(identifierCandidate);
                else if (typeCandidate != null)
                    results.Add(typeCandidate);
            }

            // Conditions are alternatives: any matching type condition lets a Location pass.
            // One required code on the raw upload is enough to reuse the map. Other type
            // codes on the upload do not lower that. Cleanup cannot add a missing code
            // before acquisition evaluates org mapping.
            var mapTypeKeys = ScoreableTypeKeys(covered, codeKeys);
            mapTypeKeys.AddRange(aliasKeys);
            if (rawTypeKeys.Count == 0 || mapTypeKeys.Count == 0)
            {
                AddBest(neededIdentifiers.Count == 0
                    ? null
                    : ToCandidate(
                        template,
                        score: 0,
                        "This map matches Location.type. Acquisition decides org membership before cleanup copies identifiers onto type, so this upload would not match as-is. Extending adds identifier conditions from the raw Locations.",
                        forceExtend: true));
                continue;
            }

            var satisfied = mapTypeKeys.Count(mapKey => MapTypeKeySatisfied(mapKey, rawTypeKeys, rawLocations));
            if (satisfied == 0)
            {
                // The upload has type codes this map does not match. Extending adds those
                // codes. An upload with no identifiers and no type codes is omitted above.
                AddBest(ToCandidate(
                    template,
                    score: 0,
                    neededIdentifiers.Count == 0
                        ? "This map matches Location.type codes that are not on the uploaded Locations. Extending adds the type codes already present on the upload."
                        : "This map matches Location.type codes that are not on the uploaded Locations. Acquisition will not see codes cleanup adds later. Extending adds identifier conditions from the raw Locations.",
                    forceExtend: true));
                continue;
            }

            var coveredTypes = rawTypeKeys.Count(raw => RawTypeCovered(raw, covered, codeKeys, aliasKeys, rawLocations));
            var coverage = (double)coveredTypes / rawTypeKeys.Count;
            AddBest(ToCandidate(
                template,
                coverage,
                "This map matches type codes already present on the uploaded Locations, which acquisition can see.",
                reuse: true));
        }

        return results
            .OrderByDescending(r => r.Recommendation == "Reuse")
            .ThenByDescending(r => r.Score)
            .ThenBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
            .Take(5)
            .ToList();
    }

    private static ReuseCandidate ToCandidate(
        OrganizationResourceMapTemplate template,
        double score,
        string reason,
        bool forceExtend = false,
        bool reuse = false)
        => new()
        {
            Id = template.Id,
            Name = template.Name,
            Kind = template.IsSystem ? "System ORM" : "Custom ORM",
            Score = Math.Round(score, 2),
            Recommendation = !forceExtend && (reuse || score >= 0.999) ? "Reuse" : "Extend",
            Reason = reason
        };

    private static HashSet<string> NeededIdentifierKeys(BundleConfigFingerprint fingerprint)
    {
        var keys = new HashSet<string>(TypeCodeKeyComparer.Instance);
        foreach (var identifier in fingerprint.LocationIdentifiers)
        {
            var system = identifier.System?.Trim() ?? "";
            var value = identifier.Value ?? "";
            if (string.IsNullOrWhiteSpace(system))
                continue;
            keys.Add(value.Length == 0
                ? IdSysKey(system)
                : IdKey(system, value));
        }

        return keys;
    }

    private static HashSet<string> NeededRawTypeKeys(BundleConfigFingerprint fingerprint)
    {
        var keys = new HashSet<string>(TypeCodeKeyComparer.Instance);
        foreach (var type in fingerprint.LocationTypes.Where(t => !string.IsNullOrWhiteSpace(t.System)))
        {
            keys.Add(string.IsNullOrWhiteSpace(type.Code)
                ? TypeSysKey(type.System)
                : TypeKey(type.System, type.Code));
        }

        return keys;
    }

    private static IReadOnlyList<string> DistinctIdentifierSystems(BundleConfigFingerprint fingerprint)
        => fingerprint.LocationIdentifiers
            .Select(i => i.System?.Trim() ?? "")
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static IEnumerable<string> ParseKeys(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            yield break;

        foreach (var part in SplitOr(path))
        {
            var id = IdentifierExists.Match(part);
            if (id.Success)
            {
                // A value-specific condition must not count as covering every Location
                // that merely shares the identifier system.
                var idSystem = UnescapeFhirPathLiteral(id.Groups[1].Value);
                var idValue = id.Groups[2].Success ? UnescapeFhirPathLiteral(id.Groups[2].Value) : "";
                if (idValue.Length > 0 && !string.IsNullOrWhiteSpace(idValue))
                    yield return IdKey(idSystem, idValue);
                else
                    yield return IdSysKey(idSystem);
                continue;
            }

            var type = TypeExists.Match(part);
            if (type.Success)
            {
                // A code-specific condition must not count as covering every Location
                // that merely shares the type codesystem. Mega-patient uploads have
                // many HSLOC codes; treating the first as system-wide skipped the rest
                // and left most Locations out-of-org. An alias predicate is part of the
                // same condition, so the type code alone must not satisfy it.
                // System, code, value, and alias equality use the unescaped literal.
                // Group presence keeps an explicit alias = '' predicate. Length would
                // drop it and score the type code as if no alias were required.
                var hasAlias = type.Groups[3].Success;
                var alias = hasAlias ? UnescapeFhirPathLiteral(type.Groups[3].Value) : "";
                var system = UnescapeFhirPathLiteral(type.Groups[1].Value);
                var code = type.Groups[2].Success ? UnescapeFhirPathLiteral(type.Groups[2].Value) : "";
                if (code.Length > 0 && !string.IsNullOrWhiteSpace(code))
                {
                    yield return hasAlias
                        ? TypeAliasKey(system, code, alias)
                        : TypeKey(system, code);
                }
                else
                {
                    yield return hasAlias
                        ? TypeSysAliasKey(system, alias)
                        : TypeSysKey(system);
                }
            }
        }
    }

    private static string UnescapeFhirPathLiteral(string literal)
    {
        if (literal.IndexOf('\\') < 0)
            return literal;

        var decoded = new StringBuilder(literal.Length);
        for (var i = 0; i < literal.Length; i++)
        {
            if (literal[i] != '\\' || i + 1 >= literal.Length)
            {
                decoded.Append(literal[i]);
                continue;
            }

            var next = literal[++i];
            switch (next)
            {
                case '\'':
                case '\\':
                case '/':
                    decoded.Append(next);
                    break;
                case 'f':
                    decoded.Append('\f');
                    break;
                case 'n':
                    decoded.Append('\n');
                    break;
                case 'r':
                    decoded.Append('\r');
                    break;
                case 't':
                    decoded.Append('\t');
                    break;
                case 'u' when i + 4 < literal.Length
                    && ushort.TryParse(literal.AsSpan(i + 1, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var code):
                    decoded.Append((char)code);
                    i += 4;
                    break;
                default:
                    decoded.Append('\\');
                    decoded.Append(next);
                    break;
            }
        }

        return decoded.ToString();
    }

    private static bool CoversIdentifierSystem(string path, string system)
        => ParseKeys(path).Any(k =>
            k.Equals(IdSysKey(system), StringComparison.OrdinalIgnoreCase));

    private static bool CoversType(string path, string system, string? code)
    {
        var keys = ParseKeys(path).ToList();
        if (keys.Contains(TypeSysKey(system), StringComparer.OrdinalIgnoreCase))
            return true;
        return !string.IsNullOrWhiteSpace(code)
               && keys.Any(key => TypeCodeKeyComparer.Instance.Equals(key, TypeKey(system, code)));
    }

    private static bool IsCovered(string neededKey, HashSet<string> covered, HashSet<string>? valueKeys = null)
    {
        if (covered.Contains(neededKey) || (valueKeys != null && valueKeys.Contains(neededKey)))
            return true;

        // A system-level condition covers every value or code in that system.
        // A value-specific or code-specific condition does not.
        if (neededKey.StartsWith("id|", StringComparison.OrdinalIgnoreCase))
        {
            var system = KeySystem(neededKey);
            return system.Length > 0 && covered.Contains(IdSysKey(system));
        }

        if (neededKey.StartsWith("type|", StringComparison.OrdinalIgnoreCase))
        {
            var system = KeySystem(neededKey);
            return system.Length > 0 && covered.Contains(TypeSysKey(system));
        }

        return false;
    }

    private static List<string> ScoreableTypeKeys(HashSet<string> covered, HashSet<string> codeKeys)
    {
        var keys = covered.Where(IsTypeKey).Concat(codeKeys).ToList();
        var systemWide = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var key in keys)
        {
            if (key.StartsWith("typesys|", StringComparison.OrdinalIgnoreCase))
                systemWide.Add(key["typesys|".Length..]);
        }

        // A system-level condition already matches every code in that system, so a
        // code-specific row for the same system is not a second requirement.
        return keys
            .Where(key =>
            {
                if (!key.StartsWith("type|", StringComparison.OrdinalIgnoreCase))
                    return true;
                var system = KeySystem(key);
                return system.Length == 0 || !systemWide.Contains(system);
            })
            .ToList();
    }

    private static bool IdentifierKeySatisfied(string mapKey, HashSet<string> neededIdentifiers)
    {
        if (neededIdentifiers.Contains(mapKey))
            return true;

        if (!mapKey.StartsWith("idsys|", StringComparison.OrdinalIgnoreCase))
            return false;

        var system = KeySystem(mapKey);
        return neededIdentifiers.Any(needed =>
            needed.StartsWith("id|", StringComparison.OrdinalIgnoreCase)
            && KeySystem(needed).Equals(system, StringComparison.OrdinalIgnoreCase));
    }

    private static bool UncoveredIdentifiersAreSiblingValues(
        HashSet<string> needed,
        HashSet<string> covered,
        HashSet<string> valueKeys)
    {
        foreach (var neededKey in needed)
        {
            if (IsCovered(neededKey, covered, valueKeys))
                continue;
            if (!neededKey.StartsWith("id|", StringComparison.OrdinalIgnoreCase))
                return false;

            var system = KeySystem(neededKey);
            var matchedValueOnSystem = valueKeys.Any(key =>
                KeySystem(key).Equals(system, StringComparison.OrdinalIgnoreCase)
                && needed.Contains(key));
            if (!matchedValueOnSystem)
                return false;
        }

        return true;
    }

    private static bool IsAliasKey(string key)
        => key.StartsWith("typealias|", StringComparison.Ordinal)
           || key.StartsWith("typesysalias|", StringComparison.Ordinal);

    private static bool RawTypeCovered(
        string rawKey,
        HashSet<string> covered,
        HashSet<string> codeKeys,
        HashSet<string> aliasKeys,
        IReadOnlyList<RawLocationHint> locations)
    {
        if (IsCovered(rawKey, covered) || codeKeys.Contains(rawKey))
            return true;

        foreach (var key in aliasKeys)
        {
            if (!TrySplitAliasKey(key, out var typeKey, out var alias))
                continue;

            if (TypeCodeKeyComparer.Instance.Equals(typeKey, rawKey))
            {
                if (locations.Any(location => AliasConditionMatches(location, typeKey, alias)))
                    return true;
                continue;
            }

            // A system-level alias covers a code only on a Location that also has that alias.
            if (typeKey.StartsWith("typesys|", StringComparison.OrdinalIgnoreCase)
                && KeySystem(rawKey).Equals(KeySystem(typeKey), StringComparison.OrdinalIgnoreCase)
                && locations.Any(location =>
                    AliasConditionMatches(location, typeKey, alias)
                    && TypeOnLocation(rawKey, location)))
                return true;
        }

        return false;
    }

    private static bool MapTypeKeySatisfied(
        string mapKey,
        HashSet<string> rawTypeKeys,
        IReadOnlyList<RawLocationHint> locations)
    {
        if (TrySplitAliasKey(mapKey, out var typeKey, out var alias))
            return locations.Any(location => AliasConditionMatches(location, typeKey, alias));

        return TypeKeySatisfied(mapKey, rawTypeKeys);
    }

    private static bool AliasConditionMatches(RawLocationHint location, string typeKey, string alias)
        => location.Aliases.Contains(alias) && TypeOnLocation(typeKey, location);

    private static bool TypeOnLocation(string typeKey, RawLocationHint location)
    {
        if (typeKey.StartsWith("typesys|", StringComparison.OrdinalIgnoreCase))
        {
            var system = typeKey["typesys|".Length..];
            return location.Types.Any(type =>
                type.System.Equals(system, StringComparison.OrdinalIgnoreCase));
        }

        var parts = SplitKey(typeKey);
        if (parts.Count < 3 || !parts[0].Equals("type", StringComparison.OrdinalIgnoreCase))
            return false;
        return location.Types.Any(type =>
            type.System.Equals(parts[1], StringComparison.OrdinalIgnoreCase)
            && type.Code.Equals(parts[2], StringComparison.Ordinal));
    }

    private static bool TypeKeySatisfied(string mapKey, HashSet<string> rawTypeKeys)
    {
        if (rawTypeKeys.Contains(mapKey))
            return true;

        if (!mapKey.StartsWith("typesys|", StringComparison.OrdinalIgnoreCase))
            return false;

        var system = KeySystem(mapKey);
        return rawTypeKeys.Any(raw =>
            raw.StartsWith("type|", StringComparison.OrdinalIgnoreCase)
            && KeySystem(raw).Equals(system, StringComparison.OrdinalIgnoreCase));
    }

    private static bool TrySplitAliasKey(string mapKey, out string typeKey, out string alias)
    {
        const string codePrefix = "typealias|";
        const string systemPrefix = "typesysalias|";
        var parts = SplitKey(mapKey);
        if (parts.Count == 4 && parts[0].Equals(codePrefix.TrimEnd('|'), StringComparison.OrdinalIgnoreCase))
        {
            typeKey = TypeKey(parts[1], parts[2]);
            alias = parts[3];
            return true;
        }

        if (parts.Count == 3 && parts[0].Equals(systemPrefix.TrimEnd('|'), StringComparison.OrdinalIgnoreCase))
        {
            typeKey = TypeSysKey(parts[1]);
            alias = parts[2];
            return true;
        }

        typeKey = "";
        alias = "";
        return false;
    }

    private static string KeySystem(string key)
    {
        var parts = SplitKey(key);
        return parts.Count >= 2 ? parts[1] : "";
    }

    private static string IdKey(string system, string value)
        => JoinKey("id", system, value);

    private static string IdSysKey(string system)
        => JoinKey("idsys", system);

    private static string TypeKey(string system, string code)
        => JoinKey("type", system, code);

    private static string TypeSysKey(string system)
        => JoinKey("typesys", system);

    private static string TypeAliasKey(string system, string code, string alias)
        => JoinKey("typealias", system, code, alias);

    private static string TypeSysAliasKey(string system, string alias)
        => JoinKey("typesysalias", system, alias);

    private static string JoinKey(string kind, params string[] parts)
        => kind + "|" + string.Join("|", parts.Select(EncodeKeyPart));

    private static string EncodeKeyPart(string value)
    {
        if (value.IndexOf('\\') < 0 && value.IndexOf('|') < 0)
            return value;
        return value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("|", "\\|", StringComparison.Ordinal);
    }

    private static List<string> SplitKey(string key)
    {
        var parts = new List<string>();
        var current = new StringBuilder();
        for (var i = 0; i < key.Length; i++)
        {
            if (key[i] == '\\' && i + 1 < key.Length)
            {
                current.Append(key[++i]);
                continue;
            }

            if (key[i] == '|')
            {
                parts.Add(current.ToString());
                current.Clear();
                continue;
            }

            current.Append(key[i]);
        }

        parts.Add(current.ToString());
        return parts;
    }

    private static bool IsIdentifierKey(string key)
        => key.StartsWith("idsys|", StringComparison.OrdinalIgnoreCase)
           || key.StartsWith("id|", StringComparison.OrdinalIgnoreCase);

    private static bool IsTypeKey(string key)
        => key.StartsWith("typesys|", StringComparison.OrdinalIgnoreCase)
           || key.StartsWith("type|", StringComparison.OrdinalIgnoreCase)
           || key.StartsWith("typesysalias|", StringComparison.OrdinalIgnoreCase)
           || key.StartsWith("typealias|", StringComparison.OrdinalIgnoreCase);

    private static IEnumerable<string> SplitOr(string path)
    {
        var parts = new List<string>();
        var start = 0;
        var inQuote = false;
        for (var i = 0; i < path.Length; i++)
        {
            if (path[i] == '\'' && !IsEscapedQuote(path, i))
            {
                inQuote = !inQuote;
                continue;
            }

            if (inQuote || !TryMatchOrSeparator(path, i, out var length))
                continue;

            AddPart(parts, path[start..i]);
            i += length - 1;
            start = i + 1;
        }

        AddPart(parts, path[start..]);
        return parts;
    }

    private static bool IsEscapedQuote(string path, int quoteIndex)
    {
        var slashes = 0;
        for (var i = quoteIndex - 1; i >= 0 && path[i] == '\\'; i--)
            slashes++;
        return slashes % 2 == 1;
    }

    private static bool TryMatchOrSeparator(string path, int index, out int length)
    {
        ReadOnlySpan<string> separators = [" or ", " OR ", " || "];
        foreach (var separator in separators)
        {
            if (index + separator.Length <= path.Length
                && path.AsSpan(index, separator.Length).SequenceEqual(separator))
            {
                length = separator.Length;
                return true;
            }
        }

        length = 0;
        return false;
    }

    private static void AddPart(List<string> parts, string value)
    {
        var trimmed = value.Trim();
        if (trimmed.Length > 0)
            parts.Add(trimmed);
    }

    private static string Escape(string value)
        => value.Replace("\\", "\\\\").Replace("'", "\\'");

    // FHIRPath code equality is case-sensitive. The system portion stays
    // ignore-case so a system-level condition still matches the upload.
    private sealed class TypeCodeKeyComparer : IEqualityComparer<string>
    {
        public static readonly TypeCodeKeyComparer Instance = new();

        public bool Equals(string? x, string? y)
        {
            if (ReferenceEquals(x, y))
                return true;
            if (x == null || y == null)
                return false;

            var left = Split(x);
            var right = Split(y);
            return left.Kind.Equals(right.Kind, StringComparison.OrdinalIgnoreCase)
                && left.System.Equals(right.System, StringComparison.OrdinalIgnoreCase)
                && left.Code.Equals(right.Code, StringComparison.Ordinal);
        }

        public int GetHashCode(string obj)
        {
            var parts = Split(obj);
            return HashCode.Combine(
                StringComparer.OrdinalIgnoreCase.GetHashCode(parts.Kind),
                StringComparer.OrdinalIgnoreCase.GetHashCode(parts.System),
                StringComparer.Ordinal.GetHashCode(parts.Code));
        }

        private static (string Kind, string System, string Code) Split(string key)
        {
            var parts = SplitKey(key);
            return (
                parts.Count > 0 ? parts[0] : "",
                parts.Count > 1 ? parts[1] : "",
                parts.Count > 2 ? parts[2] : "");
        }
    }
}
