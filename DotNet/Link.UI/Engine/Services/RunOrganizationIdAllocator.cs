using System.Globalization;

namespace Automation.UI.Services;

/// <summary>
/// Draws the five-digit id one run uses as both its facility id and its NHSN
/// organization id. The caller supplies ids that are already taken: facilities
/// that exist in Tenant, and ids ongoing runs already hold.
/// </summary>
internal static class RunOrganizationIdAllocator
{
    public const int Minimum = 10_000;
    public const int ExclusiveMaximum = 100_000;
    public const int MaxAttempts = 64;

    /// <summary>
    /// The run id when numeric-only facility ids are off. A five-digit id that is
    /// not in <paramref name="takenOrganizationIds"/> when they are on. That range
    /// is 10000 through 99999, which satisfies Tenant's numeric facility id rule.
    /// </summary>
    public static string Choose(
        bool numericOnlyFacilityId,
        Guid runId,
        IReadOnlySet<string> takenOrganizationIds,
        Func<int, int, int>? next = null)
    {
        if (!numericOnlyFacilityId)
            return runId.ToString();

        return Allocate(takenOrganizationIds, next);
    }

    public static string Allocate(IReadOnlySet<string> inUse, Func<int, int, int>? next = null)
    {
        ArgumentNullException.ThrowIfNull(inUse);
        var draw = next ?? ((min, exclusiveMax) => Random.Shared.Next(min, exclusiveMax));
        for (var attempt = 0; attempt < MaxAttempts; attempt++)
        {
            var value = draw(Minimum, ExclusiveMaximum);
            if (value < Minimum || value >= ExclusiveMaximum)
                continue;

            var candidate = value.ToString(CultureInfo.InvariantCulture);
            if (!inUse.Contains(candidate))
                return candidate;
        }

        throw new InvalidOperationException(
            "Could not allocate a five-digit NHSN organization id that no existing facility or ongoing run is using.");
    }

    /// <summary>
    /// Adds facility ids Tenant already has. Blank ids are ignored. The same set
    /// still holds ongoing runs from <see cref="CollectInUse"/>.
    /// </summary>
    public static HashSet<string> WithExistingFacilities(
        HashSet<string> inUse,
        IEnumerable<string> existingFacilityIds)
    {
        ArgumentNullException.ThrowIfNull(inUse);
        ArgumentNullException.ThrowIfNull(existingFacilityIds);
        foreach (var facilityId in existingFacilityIds)
        {
            if (!string.IsNullOrWhiteSpace(facilityId))
                inUse.Add(facilityId);
        }

        return inUse;
    }

    public static HashSet<string> CollectInUse(
        IEnumerable<(string? FacilityId, string? OrganizationId, bool Ongoing)> runs)
    {
        ArgumentNullException.ThrowIfNull(runs);
        var inUse = new HashSet<string>(StringComparer.Ordinal);
        foreach (var run in runs)
        {
            if (!run.Ongoing)
                continue;

            Add(run.FacilityId);
            Add(run.OrganizationId);
        }

        return inUse;

        void Add(string? id)
        {
            if (!string.IsNullOrWhiteSpace(id))
                inUse.Add(id);
        }
    }
}
