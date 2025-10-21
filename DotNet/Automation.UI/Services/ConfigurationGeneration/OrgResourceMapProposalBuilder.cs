using System.Text.RegularExpressions;
using Automation.UI.Models;

namespace Automation.UI.Services.ConfigurationGeneration;

public static class OrgResourceMapProposalBuilder
{
    private static readonly Regex IdentifierExists = new(
        @"^(?:Location\.)?identifier\.(?:exists|where)\(\s*system\s*=\s*'([^']+)'(?:\s+and\s+value\s*=\s*'([^']+)')?\s*\)(?:\.exists\(\s*\))?$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex TypeExists = new(
        @"^(?:Location\.)?type\.coding\.(?:exists|where)\(\s*system\s*=\s*'([^']+)'(?:\s+and\s+code\s*=\s*'([^']+)')?\s*\)(?:\.exists\(\s*\))?(?:\s+and\s+Location\.alias\s*=\s*'[^']*')?$",
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
        var results = new List<ReuseCandidate>();

        foreach (var template in existing)
        {
            var covered = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var condition in template.Conditions)
            {
                foreach (var key in ParseKeys(condition.FhirPath))
                    covered.Add(key);
            }

            var hasIdentifier = covered.Any(IsIdentifierKey);
            var hasType = covered.Any(IsTypeKey);

            if (hasIdentifier)
            {
                if (neededIdentifiers.Count == 0)
                    continue;

                var hit = neededIdentifiers.Count(neededKey => IsCovered(neededKey, covered));
                if (hit == 0)
                    continue;

                var score = (double)hit / neededIdentifiers.Count;
                results.Add(ToCandidate(
                    template,
                    score,
                    score >= 0.999
                        ? "This map already matches the Location identifiers acquisition will see."
                        : template.IsSystem
                            ? $"This system map matches {hit} of {neededIdentifiers.Count} Location identifiers from the upload. Extending clones a custom copy so the system map stays unchanged."
                            : $"This map matches {hit} of {neededIdentifiers.Count} Location identifiers from the upload."));
                continue;
            }

            if (!hasType)
                continue;

            // Type-only maps are reusable only when those type codes are already on the raw
            // upload. Cleanup cannot make them true in time for org mapping.
            if (rawTypeKeys.Count == 0)
            {
                if (neededIdentifiers.Count == 0)
                    continue;

                results.Add(ToCandidate(
                    template,
                    score: 0,
                    "This map matches Location.type. Acquisition decides org membership before cleanup copies identifiers onto type, so this upload would not match as-is. Extending adds identifier conditions from the raw Locations.",
                    forceExtend: true));
                continue;
            }

            var typeHit = rawTypeKeys.Count(neededKey => IsCovered(neededKey, covered));
            if (typeHit == 0)
            {
                if (neededIdentifiers.Count == 0)
                    continue;

                results.Add(ToCandidate(
                    template,
                    score: 0,
                    "This map matches Location.type codes that are not on the uploaded Locations. Acquisition will not see codes cleanup adds later. Extending adds identifier conditions from the raw Locations.",
                    forceExtend: true));
                continue;
            }

            var typeScore = (double)typeHit / rawTypeKeys.Count;
            results.Add(ToCandidate(
                template,
                typeScore,
                typeScore >= 0.999
                    ? "This map matches type codes already present on the uploaded Locations, which acquisition can see."
                    : $"This map matches {typeHit} of {rawTypeKeys.Count} type codes already on the uploaded Locations."));
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
        bool forceExtend = false)
        => new()
        {
            Id = template.Id,
            Name = template.Name,
            Kind = template.IsSystem ? "System ORM" : "Custom ORM",
            Score = Math.Round(score, 2),
            Recommendation = !forceExtend && score >= 0.999 ? "Reuse" : "Extend",
            Reason = reason
        };

    private static HashSet<string> NeededIdentifierKeys(BundleConfigFingerprint fingerprint)
    {
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var identifier in fingerprint.LocationIdentifiers)
        {
            var system = identifier.System?.Trim() ?? "";
            var value = identifier.Value?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(system))
                continue;
            keys.Add(string.IsNullOrWhiteSpace(value)
                ? $"idsys|{system}"
                : $"id|{system}|{value}");
        }

        return keys;
    }

    private static HashSet<string> NeededRawTypeKeys(BundleConfigFingerprint fingerprint)
    {
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var type in fingerprint.LocationTypes.Where(t => !string.IsNullOrWhiteSpace(t.System)))
        {
            keys.Add(string.IsNullOrWhiteSpace(type.Code)
                ? $"typesys|{type.System}"
                : $"type|{type.System}|{type.Code}");
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
                if (id.Groups[2].Success && !string.IsNullOrWhiteSpace(id.Groups[2].Value))
                    yield return $"id|{id.Groups[1].Value}|{id.Groups[2].Value}";
                else
                    yield return $"idsys|{id.Groups[1].Value}";
                continue;
            }

            var type = TypeExists.Match(part);
            if (type.Success)
            {
                // A code-specific condition must not count as covering every Location
                // that merely shares the type codesystem. Mega-patient uploads have
                // many HSLOC codes; treating the first as system-wide skipped the rest
                // and left most Locations out-of-org.
                if (type.Groups[2].Success && !string.IsNullOrWhiteSpace(type.Groups[2].Value))
                    yield return $"type|{type.Groups[1].Value}|{type.Groups[2].Value}";
                else
                    yield return $"typesys|{type.Groups[1].Value}";
            }
        }
    }

    private static bool CoversIdentifierSystem(string path, string system)
        => ParseKeys(path).Any(k =>
            k.Equals($"idsys|{system}", StringComparison.OrdinalIgnoreCase));

    private static bool CoversType(string path, string system, string? code)
    {
        var keys = ParseKeys(path).ToList();
        if (keys.Contains($"typesys|{system}", StringComparer.OrdinalIgnoreCase))
            return true;
        return !string.IsNullOrWhiteSpace(code)
               && keys.Contains($"type|{system}|{code}", StringComparer.OrdinalIgnoreCase);
    }

    private static bool IsCovered(string neededKey, HashSet<string> covered)
    {
        if (covered.Contains(neededKey))
            return true;

        if (neededKey.StartsWith("id|", StringComparison.OrdinalIgnoreCase))
        {
            var parts = neededKey.Split('|');
            return parts.Length >= 2 && covered.Contains($"idsys|{parts[1]}");
        }

        if (neededKey.StartsWith("type|", StringComparison.OrdinalIgnoreCase))
        {
            var parts = neededKey.Split('|');
            return parts.Length >= 2 && covered.Contains($"typesys|{parts[1]}");
        }

        return false;
    }

    private static bool IsIdentifierKey(string key)
        => key.StartsWith("idsys|", StringComparison.OrdinalIgnoreCase)
           || key.StartsWith("id|", StringComparison.OrdinalIgnoreCase);

    private static bool IsTypeKey(string key)
        => key.StartsWith("typesys|", StringComparison.OrdinalIgnoreCase)
           || key.StartsWith("type|", StringComparison.OrdinalIgnoreCase);

    private static IEnumerable<string> SplitOr(string path)
        => path.Split([" or ", " OR ", " || "], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static string Escape(string value)
        => value.Replace("\\", "\\\\").Replace("'", "\\'");
}
