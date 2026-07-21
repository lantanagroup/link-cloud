using System.Text.Json;
using System.Text.RegularExpressions;

namespace LantanaGroup.Automation.Generation;

/// <summary>
/// Applies organization resource map scope to an already query-plan-predicted acquired key set.
///
/// This models org-location filtering as a post-query-plan stage:
/// 1) evaluate org-location conditions against Location resources,
/// 2) propagate org status down the partOf hierarchy,
/// 3) keep only resources linked to org-mapped encounters.
/// </summary>
public static class OrgResourceMapPredictionFilter
{
    private sealed record ResourceEntry(string ResourceType, string ResourceId, string Key, JsonElement Resource);

    public static HashSet<string> Apply(
        HashSet<string> acquiredKeys,
        IReadOnlyList<(string ResourceType, string ResourceId, string Key, JsonElement Resource)> patientResourceEntries,
        IReadOnlyList<(string ResourceType, string ResourceId, string Key, JsonElement Resource)>? sharedResourceEntries,
        IReadOnlyList<string>? organizationLocationConditionFhirPaths)
    {
        if (acquiredKeys.Count == 0
            || organizationLocationConditionFhirPaths == null
            || organizationLocationConditionFhirPaths.Count == 0)
        {
            return acquiredKeys;
        }

        var allEntries = patientResourceEntries
            .Concat(sharedResourceEntries ?? [])
            .Select(e => new ResourceEntry(e.ResourceType, e.ResourceId, e.Key, e.Resource))
            .ToList();

        var entriesByKey = allEntries
            .Where(e => !string.IsNullOrWhiteSpace(e.Key))
            .GroupBy(e => e.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var locations = allEntries
            .Where(e => string.Equals(e.ResourceType, "Location", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var encounters = allEntries
            .Where(e => string.Equals(e.ResourceType, "Encounter", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (locations.Count == 0 || encounters.Count == 0)
            return acquiredKeys;

        var directOrgLocationIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var location in locations)
        {
            if (organizationLocationConditionFhirPaths.Any(path => LocationMatchesCondition(location.Resource, path)))
                directOrgLocationIds.Add(location.ResourceId);
        }

        if (directOrgLocationIds.Count == 0)
            return [];

        var effectiveOrgLocationIds = new HashSet<string>(directOrgLocationIds, StringComparer.OrdinalIgnoreCase);
        var changed = true;
        while (changed)
        {
            changed = false;
            foreach (var location in locations)
            {
                if (effectiveOrgLocationIds.Contains(location.ResourceId))
                    continue;

                if (!TryGetReferencedResourceIds(location.Resource, "Location", out var parentIds))
                    continue;

                if (!parentIds.Any(effectiveOrgLocationIds.Contains))
                    continue;

                effectiveOrgLocationIds.Add(location.ResourceId);
                changed = true;
            }
        }

        var orgEncounterIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var orgEncounterLocationIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var encounter in encounters)
        {
            if (!TryGetReferencedResourceIds(encounter.Resource, "Location", out var locationIds))
                continue;

            var isOrgEncounter = false;
            foreach (var locationId in locationIds)
            {
                if (!effectiveOrgLocationIds.Contains(locationId))
                    continue;

                isOrgEncounter = true;
                orgEncounterLocationIds.Add(locationId);
            }

            if (isOrgEncounter)
                orgEncounterIds.Add(encounter.ResourceId);
        }

        var filtered = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var key in acquiredKeys)
        {
            if (!entriesByKey.TryGetValue(key, out var entry))
            {
                filtered.Add(key);
                continue;
            }

            if (MatchesOrganizationScope(entry, orgEncounterIds, orgEncounterLocationIds))
                filtered.Add(key);
        }

        return PruneUnreferencedReferenceResources(filtered, entriesByKey);
    }

    private static HashSet<string> PruneUnreferencedReferenceResources(
        HashSet<string> keptKeys,
        Dictionary<string, ResourceEntry> entriesByKey)
    {
        if (keptKeys.Count == 0)
            return keptKeys;

        // These are typically acquired through reference queries and should only remain in
        // prediction if still referenced by kept resources after org-scope filtering.
        var pruneTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Medication",
            "Specimen",
            "Device"
        };

        var changed = true;
        while (changed)
        {
            changed = false;

            var referencedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var key in keptKeys)
            {
                if (!entriesByKey.TryGetValue(key, out var entry))
                    continue;

                foreach (var reference in EnumerateReferences(entry.Resource))
                {
                    if (!TryParseReference(reference, out var resourceType, out var resourceId))
                        continue;

                    var referencedKey = $"{resourceType}/{resourceId}";
                    if (keptKeys.Contains(referencedKey))
                        referencedKeys.Add(referencedKey);
                }
            }

            var toRemove = keptKeys
                .Where(key => TryGetResourceType(key, out var resourceType)
                              && pruneTypes.Contains(resourceType)
                              && !referencedKeys.Contains(key))
                .ToList();

            if (toRemove.Count == 0)
                continue;

            foreach (var key in toRemove)
                keptKeys.Remove(key);

            changed = true;
        }

        return keptKeys;
    }

    private static bool MatchesOrganizationScope(
        ResourceEntry resource,
        HashSet<string> orgEncounterIds,
        HashSet<string> orgEncounterLocationIds)
    {
        if (string.Equals(resource.ResourceType, "Encounter", StringComparison.OrdinalIgnoreCase))
            return orgEncounterIds.Contains(resource.ResourceId);

        if (string.Equals(resource.ResourceType, "Location", StringComparison.OrdinalIgnoreCase))
            return orgEncounterLocationIds.Contains(resource.ResourceId);

        if (!TryGetReferencedResourceIds(resource.Resource, "Encounter", out var encounterIds))
            return true;

        return encounterIds.Any(orgEncounterIds.Contains);
    }

    private static bool LocationMatchesCondition(JsonElement locationResource, string fhirPath)
    {
        if (string.IsNullOrWhiteSpace(fhirPath))
            return false;

        var path = fhirPath.Trim();
        if (path.StartsWith("Location.", StringComparison.OrdinalIgnoreCase))
            path = path["Location.".Length..];

        var system = ExtractQuotedValue(path, "system");
        var value = ExtractQuotedValue(path, "value");
        var code = ExtractQuotedValue(path, "code");

        if (path.Contains("identifier", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(system)
            && locationResource.TryGetProperty("identifier", out var identifiers)
            && identifiers.ValueKind == JsonValueKind.Array)
        {
            foreach (var identifier in identifiers.EnumerateArray())
            {
                if (!identifier.TryGetProperty("system", out var sysProp) || sysProp.ValueKind != JsonValueKind.String)
                    continue;

                var identifierSystem = sysProp.GetString();
                if (!string.Equals(identifierSystem, system, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (string.IsNullOrWhiteSpace(value))
                    return true;

                if (identifier.TryGetProperty("value", out var valueProp)
                    && valueProp.ValueKind == JsonValueKind.String
                    && string.Equals(valueProp.GetString(), value, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        if (path.Contains("type.coding", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(system)
            && locationResource.TryGetProperty("type", out var types)
            && types.ValueKind == JsonValueKind.Array)
        {
            foreach (var type in types.EnumerateArray())
            {
                if (!type.TryGetProperty("coding", out var codingArray) || codingArray.ValueKind != JsonValueKind.Array)
                    continue;

                foreach (var coding in codingArray.EnumerateArray())
                {
                    if (!coding.TryGetProperty("system", out var sysProp) || sysProp.ValueKind != JsonValueKind.String)
                        continue;

                    if (!string.Equals(sysProp.GetString(), system, StringComparison.OrdinalIgnoreCase))
                        continue;

                    if (string.IsNullOrWhiteSpace(code))
                        return true;

                    if (coding.TryGetProperty("code", out var codeProp)
                        && codeProp.ValueKind == JsonValueKind.String
                        && string.Equals(codeProp.GetString(), code, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    private static string? ExtractQuotedValue(string source, string key)
    {
        var match = Regex.Match(source, $@"\b{Regex.Escape(key)}\s*=\s*'([^']+)'", RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value : null;
    }

    private static bool TryGetReferencedResourceIds(JsonElement resource, string resourceType, out List<string> ids)
    {
        ids = EnumerateReferences(resource)
            .Where(reference => reference.StartsWith(resourceType + "/", StringComparison.OrdinalIgnoreCase))
            .Select(reference => reference[(resourceType.Length + 1)..])
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return ids.Count > 0;
    }

    private static bool TryGetResourceType(string key, out string resourceType)
    {
        var idx = key.IndexOf('/');
        if (idx <= 0)
        {
            resourceType = string.Empty;
            return false;
        }

        resourceType = key[..idx];
        return true;
    }

    private static bool TryParseReference(string reference, out string resourceType, out string resourceId)
    {
        resourceType = string.Empty;
        resourceId = string.Empty;

        if (string.IsNullOrWhiteSpace(reference))
            return false;

        var parts = reference.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
            return false;

        resourceType = parts[^2];
        resourceId = parts[^1];
        return !string.IsNullOrWhiteSpace(resourceType) && !string.IsNullOrWhiteSpace(resourceId);
    }

    private static IEnumerable<string> EnumerateReferences(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var prop in element.EnumerateObject())
                {
                    if (string.Equals(prop.Name, "reference", StringComparison.Ordinal)
                        && prop.Value.ValueKind == JsonValueKind.String
                        && prop.Value.GetString() is { Length: > 0 } reference)
                    {
                        yield return reference;
                    }

                    foreach (var child in EnumerateReferences(prop.Value))
                        yield return child;
                }
                break;

            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    foreach (var child in EnumerateReferences(item))
                        yield return child;
                }
                break;
        }
    }
}
