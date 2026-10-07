namespace Link.UI.Services;

/// <summary>
/// A run row the engine and the dashboard both use to decide ownership.
/// The dashboard posts these facility ids to the tenant count. It does not send them to the browser.
/// </summary>
public sealed record AutomationRunMark(
    Guid RunId,
    string? FacilityId,
    bool AutomationCreatedFacility,
    DateTimeOffset CreatedAt);

/// <summary>
/// Whether a facility is automation-owned (a throwaway) or real.
/// The decision uses durable run and tombstone rows only.
/// Automation stores the facility name as the facility id. There is no name prefix,
/// so the name is not consulted. A facility that no run and no tombstone claims is real.
/// </summary>
public static class FacilityClassification
{
    /// <summary>
    /// True when <paramref name="candidate"/> is the run id, or this run created that facility.
    /// A reused tenant (a different id, created flag false) is not owned.
    /// </summary>
    public static bool RunOwns(Guid runId, string? storedFacilityId, bool created, string? candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate))
            return false;

        var id = candidate.Trim();
        if (Guid.TryParse(id, out var parsed) && parsed == runId)
            return true;

        return created
            && !string.IsNullOrWhiteSpace(storedFacilityId)
            && string.Equals(id, storedFacilityId.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Server-side guard for teardown, purge, and quiesce.
    /// True only when a run owns the id or a tombstone still names it.
    /// </summary>
    public static bool AllowsDestructive(
        string? facilityId,
        IEnumerable<AutomationRunMark> runs,
        IEnumerable<string?>? tombstoneFacilityIds)
    {
        if (string.IsNullOrWhiteSpace(facilityId))
            return false;

        foreach (var run in runs)
        {
            if (RunOwns(run.RunId, run.FacilityId, run.AutomationCreatedFacility, facilityId))
                return true;
        }

        if (tombstoneFacilityIds is null)
            return false;

        foreach (var id in tombstoneFacilityIds)
        {
            if (string.Equals(id?.Trim(), facilityId.Trim(), StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}
