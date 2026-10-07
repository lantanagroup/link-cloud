namespace Link.UI.Services;

/// <summary>
/// Which facilities an automation run owns, and how the Tenants, Reports, and Logs
/// pages turn that into a badge and an Automation / All filter.
/// The lookup is one cached read. These rules do not call a service per row.
/// </summary>
public static class AutomationMarkRules
{
    public const string All = "all";
    public const string Automation = "automation";
    public const int MaxFacilitySearches = 40;
    public const string NotOwnedNote = "That facility is not an automation facility.";

    public static bool IsAutomation(string? scope) =>
        string.Equals(scope?.Trim(), Automation, StringComparison.OrdinalIgnoreCase);

    public static string NormalizeScope(string? scope) =>
        IsAutomation(scope) ? Automation : All;

    public static string? SearchNote(bool truncated, bool partialPages)
    {
        var truncatedNote = truncated
            ? $"Showing the {MaxFacilitySearches} newest automation facilities."
            : null;
        var partialNote = partialPages
            ? "Each facility contributes its first page. Search one facility to see the rest."
            : null;
        if (truncatedNote is null)
            return partialNote;
        if (partialNote is null)
            return truncatedNote;
        return truncatedNote + " " + partialNote;
    }

    public static string CacheKey(string kind, IReadOnlyList<string> facilityIds, string fingerprint) =>
        "au-own:" + kind + ":" + fingerprint + ":" + string.Join(",", facilityIds);

    public static AutomationOwnershipIndex Build(
        IEnumerable<AutomationRunMark> runs,
        IEnumerable<AutomationTombstoneMark> tombstones)
    {
        var claims = new Dictionary<string, Claim>(StringComparer.OrdinalIgnoreCase);
        foreach (var run in runs)
        {
            var runId = CanonicalRunId(run.RunId.ToString("D"));
            // A normal run's facility id is the run id, even when the created flag is unset.
            Consider(claims, runId, runId, run.CreatedAt, rank: 1);
            if (run.AutomationCreatedFacility)
                Consider(claims, run.FacilityId, runId, run.CreatedAt, rank: 2);
        }

        foreach (var tombstone in tombstones)
            Consider(claims, tombstone.FacilityId, CanonicalRunId(tombstone.RunId), tombstone.CreatedAt, rank: 0);

        var newest = claims
            .OrderByDescending(pair => pair.Value.SeenAt)
            .ThenBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
            .Select(pair => pair.Key)
            .ToList();
        var runByFacility = claims.ToDictionary(
            pair => pair.Key,
            pair => pair.Value.RunId,
            StringComparer.OrdinalIgnoreCase);
        return new AutomationOwnershipIndex(newest, runByFacility);
    }

    public static RowPage<T> Slice<T>(IReadOnlyList<T> items, int page, int pageSize)
    {
        var size = pageSize < 1 ? 1 : pageSize;
        var total = items.Count;
        var pages = total == 0 ? 0 : (int)Math.Ceiling(total / (double)size);
        var number = pages == 0 ? 1 : Math.Clamp(page < 1 ? 1 : page, 1, pages);
        var start = (number - 1) * size;
        return new RowPage<T>(items.Skip(start).Take(size).ToList(), number, size, total, pages);
    }

    private static void Consider(
        Dictionary<string, Claim> claims,
        string? facilityId,
        string? runId,
        DateTimeOffset seenAt,
        int rank)
    {
        if (string.IsNullOrWhiteSpace(facilityId) || string.IsNullOrWhiteSpace(runId))
            return;

        var key = facilityId.Trim();
        var canonical = runId.Trim();
        if (!claims.TryGetValue(key, out var current)
            || rank > current.Rank
            || (rank == current.Rank && seenAt > current.SeenAt)
            || (rank == current.Rank && seenAt == current.SeenAt && string.CompareOrdinal(canonical, current.RunId) > 0))
        {
            claims[key] = new Claim(canonical, seenAt, rank);
        }
    }

    private static string CanonicalRunId(string? runId)
    {
        if (string.IsNullOrWhiteSpace(runId))
            return string.Empty;
        return Guid.TryParse(runId, out var id) ? id.ToString("D") : runId.Trim();
    }

    private readonly record struct Claim(string RunId, DateTimeOffset SeenAt, int Rank);
}

public sealed record AutomationRunMark(
    Guid RunId,
    string? FacilityId,
    bool AutomationCreatedFacility,
    DateTimeOffset CreatedAt);

public sealed record AutomationTombstoneMark(string FacilityId, string RunId, DateTimeOffset CreatedAt);

public sealed record RowPage<T>(IReadOnlyList<T> Items, int Page, int Size, int Total, int Pages);

public sealed class AutomationOwnershipLoad
{
    public bool Reachable { get; init; }
    public IReadOnlyList<AutomationRunMark> Runs { get; init; } = [];
    public IReadOnlyList<AutomationTombstoneMark> Tombstones { get; init; } = [];
}

public sealed class AutomationOwnershipIndex
{
    public static AutomationOwnershipIndex Empty { get; } = new([], new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));

    private readonly IReadOnlyList<string> _newestFacilityIds;
    private readonly Dictionary<string, string> _runByFacility;

    public AutomationOwnershipIndex(IReadOnlyList<string> newestFacilityIds, Dictionary<string, string> runByFacility)
    {
        _newestFacilityIds = newestFacilityIds;
        _runByFacility = runByFacility;
    }

    public bool Contains(string? facilityId) => RunIdFor(facilityId) is not null;

    public string? RunIdFor(string? facilityId)
    {
        if (string.IsNullOrWhiteSpace(facilityId))
            return null;
        return _runByFacility.TryGetValue(facilityId.Trim(), out var runId) ? runId : null;
    }

    public IReadOnlyList<string> NewestFacilityIds(int max, out bool truncated)
    {
        var take = max < 1 ? 1 : max;
        truncated = _newestFacilityIds.Count > take;
        if (!truncated)
            return _newestFacilityIds;
        return _newestFacilityIds.Take(take).ToList();
    }
}
