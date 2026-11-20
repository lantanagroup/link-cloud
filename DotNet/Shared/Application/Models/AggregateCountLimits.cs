namespace LantanaGroup.Link.Shared.Application.Models;

/// <summary>
/// Bounds for the aggregate count endpoints. A facility id set travels in a POST body
/// so a query string does not have to carry thousands of ids.
/// </summary>
public static class AggregateCountLimits
{
    public const int MaxDays = 31;
    public const int DefaultDays = 7;
    public const int MaxFacilityIds = 5000;
    public const int MaxFacilityIdLength = 200;
    public const int MaxAuditHours = 168;
    public const int DefaultAuditHours = 24;

    public static bool TryDays(int days, out string? error)
    {
        if (days < 1 || days > MaxDays)
        {
            error = $"Days must be from 1 to {MaxDays}.";
            return false;
        }

        error = null;
        return true;
    }

    public static bool TryHours(int hours, out string? error)
    {
        if (hours < 1 || hours > MaxAuditHours)
        {
            error = $"Hours must be from 1 to {MaxAuditHours}.";
            return false;
        }

        error = null;
        return true;
    }

    /// <summary>
    /// Null means the caller did not send a set. An empty set is a real filter that matches nothing.
    /// </summary>
    public static bool TryFacilityIds(IReadOnlyList<string>? ids, out List<string>? normalized, out string? error)
    {
        if (ids is null)
        {
            normalized = null;
            error = null;
            return true;
        }

        if (ids.Count > MaxFacilityIds)
        {
            normalized = null;
            error = $"At most {MaxFacilityIds} facility ids are accepted.";
            return false;
        }

        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var raw in ids)
        {
            var id = raw?.Trim();
            if (string.IsNullOrEmpty(id))
                continue;

            if (id.Length > MaxFacilityIdLength || id.Contains('<') || id.Contains('>') || id.Contains('\r') || id.Contains('\n'))
            {
                normalized = null;
                error = "A facility id is not valid.";
                return false;
            }

            set.Add(id);
        }

        normalized = set.Count == 0 ? [] : set.ToList();
        error = null;
        return true;
    }
}
