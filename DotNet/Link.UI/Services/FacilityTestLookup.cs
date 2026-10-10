using LantanaGroup.Link.Sdk.ApiClient;
using LantanaGroup.Link.Sdk.Clients;
using LantanaGroup.Link.Shared.Application.Models.Tenant;

namespace Link.UI.Services;

/// <summary>
/// Which facilities are test facilities, read from Tenant with the facility page.
/// One call covers the ids on a page. A miss is not "none of these are test facilities".
/// </summary>
public sealed class FacilityTestLookup
{
    private readonly IFacilityServiceClient _facilities;
    private readonly ILogger<FacilityTestLookup> _logger;

    public FacilityTestLookup(IFacilityServiceClient facilities, ILogger<FacilityTestLookup> logger)
    {
        _facilities = facilities;
        _logger = logger;
    }

    public async Task<FacilityTestRead> ReadAsync(string? facilityId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(facilityId))
            return FacilityTestRead.Known(false);

        try
        {
            var response = await _facilities.GetAsync(facilityId.Trim(), cancellationToken);
            if (response.StatusCode == StatusCodes.Status404NotFound)
                return FacilityTestRead.Known(false);
            if (!response.IsSuccessStatusCode || response.Body is null)
                return FacilityTestRead.Miss;
            return FacilityTestRead.Known(response.Body.IsTest);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Facility test flag could not be read.");
            return FacilityTestRead.Miss;
        }
    }

    public async Task<FacilityTestIndex> ForIdsAsync(IEnumerable<string?> facilityIds, CancellationToken cancellationToken)
    {
        var ids = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var raw in facilityIds)
        {
            if (string.IsNullOrWhiteSpace(raw))
                continue;
            var id = raw.Trim();
            if (seen.Add(id))
                ids.Add(id);
        }

        if (ids.Count == 0)
            return FacilityTestIndex.None;

        try
        {
            var response = await _facilities.GetFacilityFlagsAsync(new FacilityFlagRequest { FacilityIds = ids }, cancellationToken);
            if (!response.IsSuccessStatusCode || response.Body is null)
                return FacilityTestIndex.Unreachable;
            return FacilityTestIndex.FromFlags(response.Body);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Facility test flags could not be read.");
            return FacilityTestIndex.Unreachable;
        }
    }

    public async Task<FacilityTestIndex> NewestTestFacilitiesAsync(int max, CancellationToken cancellationToken)
    {
        var take = max < 1 ? 1 : max;
        try
        {
            var response = await _facilities.SearchFacilitiesPageAsync(
                isTest: true,
                sortBy: "CreateDate",
                sortOrder: "Descending",
                pageSize: take,
                pageNumber: 1,
                cancellationToken: cancellationToken);
            if (response.StatusCode == StatusCodes.Status204NoContent)
                return FacilityTestIndex.None;
            if (!response.IsSuccessStatusCode || response.Body is null)
                return FacilityTestIndex.Unreachable;

            var ids = response.Body.Records
                .Select(facility => facility.FacilityId)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Select(id => id!.Trim())
                .ToList();
            var total = response.Body.Metadata?.TotalCount ?? ids.Count;
            return FacilityTestIndex.FromIds(ids, truncated: total > ids.Count);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Test facilities could not be listed.");
            return FacilityTestIndex.Unreachable;
        }
    }

    /// <summary>
    /// Chooses the facility set for a Reports or Logs page.
    /// With automation disabled the page is the caller's normal query and nothing is dropped.
    /// </summary>
    public async Task<FacilityPageScope> ResolvePageAsync(
        bool automationEnabled,
        string? scope,
        string? facilityId,
        CancellationToken cancellationToken)
    {
        if (!automationEnabled)
            return new FacilityPageScope { UseNamedFacility = true };

        var facility = string.IsNullOrWhiteSpace(facilityId) ? null : facilityId.Trim();
        if (AutomationMarkRules.IsAutomation(scope))
        {
            if (facility is not null)
            {
                var read = await ReadAsync(facility, cancellationToken);
                if (!read.Reachable)
                    return new FacilityPageScope { EmptyNote = AutomationMarkRules.OwnershipUnreachable };
                if (!read.IsTest)
                    return new FacilityPageScope { EmptyNote = AutomationMarkRules.NotOwnedNote };
                return new FacilityPageScope { UseNamedFacility = true, KnownTest = true };
            }

            var newest = await NewestTestFacilitiesAsync(AutomationMarkRules.MaxFacilitySearches, cancellationToken);
            if (!newest.Reachable)
                return new FacilityPageScope { EmptyNote = AutomationMarkRules.OwnershipUnreachable };
            return new FacilityPageScope
            {
                FacilityIds = newest.Ids,
                Truncated = newest.Truncated,
                KnownTest = true
            };
        }

        if (facility is not null && AutomationMarkRules.IsReal(scope))
        {
            var read = await ReadAsync(facility, cancellationToken);
            if (!read.Reachable)
                return new FacilityPageScope { EmptyNote = AutomationMarkRules.OwnershipUnreachable };
            if (read.IsTest)
                return new FacilityPageScope { EmptyNote = AutomationMarkRules.OwnedFacilityNote };
            return new FacilityPageScope { UseNamedFacility = true };
        }

        return new FacilityPageScope
        {
            UseNamedFacility = true,
            DropTestRows = AutomationMarkRules.IsReal(scope)
        };
    }
}

public readonly record struct FacilityTestRead(bool Reachable, bool IsTest)
{
    public static FacilityTestRead Miss { get; } = new(false, false);

    public static FacilityTestRead Known(bool isTest) => new(true, isTest);
}

/// <summary>
/// The test facilities in one Tenant answer. <see cref="Reachable"/> false means the answer is missing.
/// </summary>
public sealed class FacilityTestIndex
{
    public static FacilityTestIndex None { get; } = new(true, [], false);

    public static FacilityTestIndex Unreachable { get; } = new(false, [], false);

    private readonly HashSet<string> _testIds;

    private FacilityTestIndex(bool reachable, IReadOnlyList<string> ids, bool truncated)
    {
        Reachable = reachable;
        Ids = ids;
        Truncated = truncated;
        _testIds = new HashSet<string>(ids, StringComparer.OrdinalIgnoreCase);
    }

    public bool Reachable { get; }

    public bool Truncated { get; }

    public IReadOnlyList<string> Ids { get; }

    public bool IsTest(string? facilityId) =>
        !string.IsNullOrWhiteSpace(facilityId) && _testIds.Contains(facilityId.Trim());

    public static FacilityTestIndex FromFlags(IEnumerable<FacilityFlag> flags)
    {
        var ids = flags
            .Where(flag => flag.IsTest && !string.IsNullOrWhiteSpace(flag.FacilityId))
            .Select(flag => flag.FacilityId.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        return new FacilityTestIndex(true, ids, false);
    }

    public static FacilityTestIndex FromIds(IReadOnlyList<string> ids, bool truncated) =>
        new(true, ids, truncated);
}

/// <summary>
/// How one list page should load once the caller knows whether automation chrome is on.
/// The flag read is one Tenant call, not one call per row.
/// </summary>
public sealed class FacilityPageScope
{
    public bool UseNamedFacility { get; init; }

    /// <summary>Every row on this page belongs to a test facility. The caller does not need a second read to badge them.</summary>
    public bool KnownTest { get; init; }

    public bool DropTestRows { get; init; }

    public IReadOnlyList<string> FacilityIds { get; init; } = [];

    public bool Truncated { get; init; }

    public string? EmptyNote { get; init; }
}
