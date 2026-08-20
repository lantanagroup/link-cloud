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
            SuggestedDescription = "Auto-built so every Location found in the uploaded bundle can pass org-location mapping (any-match)."
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
            proposal.Notes.Add($"Proposed {conditions.Count} match condition(s). Any matching condition lets a Location pass, so every distinct identifier system{(needTypeFallback ? " or type" : "")} from the upload is covered.");

        if (fingerprint.LocationsWithoutIdentifier > 0)
            proposal.Notes.Add($"{fingerprint.LocationsWithoutIdentifier} Location(s) had no identifier. Type-coding conditions were added so those can still pass.");

        proposal.Reuse = ScoreExisting(fingerprint, existing);
        return proposal;
    }

    public static List<ReuseCandidate> ScoreExisting(
        BundleConfigFingerprint fingerprint,
        IReadOnlyList<OrganizationResourceMapTemplate> existing)
    {
        var needed = NeededKeys(fingerprint);
        if (needed.Count == 0)
            return [];

        var results = new List<ReuseCandidate>();
        foreach (var template in existing)
        {
            var covered = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var condition in template.Conditions)
            {
                foreach (var key in ParseKeys(condition.FhirPath))
                    covered.Add(key);
            }

            var hit = needed.Count(neededKey => IsCovered(neededKey, covered));
            if (hit == 0)
                continue;

            var score = (double)hit / needed.Count;
            results.Add(new ReuseCandidate
            {
                Id = template.Id,
                Name = template.Name,
                Kind = template.IsSystem ? "System ORM" : "Custom ORM",
                Score = Math.Round(score, 2),
                Recommendation = score >= 0.999 ? "Reuse" : "Extend",
                Reason = score >= 0.999
                    ? "This map already covers every Location identifier/type found in the upload."
                    : template.IsSystem
                        ? $"This system map covers {hit} of {needed.Count} Location fingerprints. Extending clones a custom copy so the system map stays unchanged."
                        : $"This map covers {hit} of {needed.Count} Location fingerprints. Extending it would keep one map for all uploaded patients."
            });
        }

        return results
            .OrderByDescending(r => r.Score)
            .ThenBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
            .Take(5)
            .ToList();
    }

    private static HashSet<string> NeededKeys(BundleConfigFingerprint fingerprint)
    {
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var systems = DistinctIdentifierSystems(fingerprint);
        foreach (var system in systems)
            keys.Add($"idsys|{system}");

        if (systems.Count == 0 || fingerprint.LocationsWithoutIdentifier > 0)
        {
            foreach (var type in fingerprint.LocationTypes.Where(t => !string.IsNullOrWhiteSpace(t.System)))
            {
                keys.Add(string.IsNullOrWhiteSpace(type.Code)
                    ? $"typesys|{type.System}"
                    : $"type|{type.System}|{type.Code}");
            }
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
                yield return $"idsys|{id.Groups[1].Value}";
                if (id.Groups[2].Success && !string.IsNullOrWhiteSpace(id.Groups[2].Value))
                    yield return $"id|{id.Groups[1].Value}|{id.Groups[2].Value}";
                continue;
            }

            var type = TypeExists.Match(part);
            if (type.Success)
            {
                yield return $"typesys|{type.Groups[1].Value}";
                if (type.Groups[2].Success && !string.IsNullOrWhiteSpace(type.Groups[2].Value))
                    yield return $"type|{type.Groups[1].Value}|{type.Groups[2].Value}";
            }
        }
    }

    private static bool CoversIdentifierSystem(string path, string system)
        => ParseKeys(path).Any(k =>
            k.Equals($"idsys|{system}", StringComparison.OrdinalIgnoreCase)
            || k.StartsWith($"id|{system}|", StringComparison.OrdinalIgnoreCase));

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

    private static IEnumerable<string> SplitOr(string path)
        => path.Split([" or ", " OR ", " || "], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static string Escape(string value)
        => value.Replace("\\", "\\\\").Replace("'", "\\'");
}
