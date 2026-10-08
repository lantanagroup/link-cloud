namespace Link.UI.Services;

/// <summary>
/// The tenant list matches facility names. An exact id that is not already in that
/// list is loaded on its own so a search by id still finds the facility.
/// </summary>
public static class TenantListSearch
{
    public static bool ShouldLookupExactId(string? search, IEnumerable<string> knownIds)
    {
        var term = search?.Trim();
        if (string.IsNullOrEmpty(term))
            return false;
        foreach (var ch in term)
        {
            if (char.IsWhiteSpace(ch) || ch is '/' or '\\' or '?' or '#')
                return false;
        }

        foreach (var id in knownIds)
        {
            if (string.Equals(id, term, StringComparison.OrdinalIgnoreCase))
                return false;
        }

        return true;
    }

    public static void AddExact(
        IDictionary<string, string> active,
        IDictionary<string, string> all,
        bool includeDeleted,
        string? facilityId,
        string? facilityName,
        bool? isDeleted)
    {
        if (string.IsNullOrWhiteSpace(facilityId))
            return;

        var deleted = isDeleted == true;
        if (deleted && (!includeDeleted || ReferenceEquals(active, all)))
            return;

        var name = string.IsNullOrWhiteSpace(facilityName) ? facilityId : facilityName;
        if (!deleted)
            Put(active, facilityId, name);
        Put(all, facilityId, name);
    }

    private static void Put(IDictionary<string, string> map, string id, string name)
    {
        foreach (var key in map.Keys)
        {
            if (string.Equals(key, id, StringComparison.OrdinalIgnoreCase))
                return;
        }

        map[id] = name;
    }
}
