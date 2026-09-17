namespace Automation.UI.Services.TestRail;

public static class TestRailCaseMapper
{
    public static int? ResolveScenarioCaseId(
        int? scenarioCaseId,
        Guid? scenarioId,
        string? scenarioName,
        IReadOnlyDictionary<string, int>? map)
    {
        if (scenarioCaseId is > 0)
            return scenarioCaseId;

        if (map is null || map.Count == 0)
            return null;

        if (scenarioId is Guid id && TryGet(map, id.ToString("D"), out var byId))
            return byId;

        if (!string.IsNullOrWhiteSpace(scenarioName) && TryGet(map, scenarioName, out var byName))
            return byName;

        return null;
    }

    public static int? ResolveApiHealthCaseId(
        int? endpointCaseId,
        string? endpointKey,
        string? endpointName,
        IReadOnlyDictionary<string, int>? map)
    {
        if (endpointCaseId is > 0)
            return endpointCaseId;

        if (map is null || map.Count == 0)
            return null;

        if (!string.IsNullOrWhiteSpace(endpointKey) && TryGet(map, endpointKey, out var byKey))
            return byKey;

        if (!string.IsNullOrWhiteSpace(endpointName) && TryGet(map, endpointName, out var byName))
            return byName;

        return null;
    }

    private static bool TryGet(IReadOnlyDictionary<string, int> map, string key, out int value)
    {
        foreach (var kvp in map)
        {
            if (string.Equals(kvp.Key, key, StringComparison.OrdinalIgnoreCase) && kvp.Value > 0)
            {
                value = kvp.Value;
                return true;
            }
        }

        value = 0;
        return false;
    }
}
