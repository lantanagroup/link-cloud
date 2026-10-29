using System.Collections.Concurrent;
using LantanaGroup.Link.Sdk.Clients;

namespace Automation.UI.Services;

/// <summary>
/// Assigns an NHSN organization id that is not reserved, not already on a
/// scenario, and not already a facility. A process-local set covers two
/// allocations that both run before either facility exists.
/// </summary>
public static class NhsnOrganizationIdAllocator
{
    private static readonly ConcurrentDictionary<string, byte> RecentlyAllocated = new(StringComparer.Ordinal);

    public static bool ShouldReplace(string? id) =>
        string.IsNullOrWhiteSpace(id) || NhsnOrganizationIds.IsReserved(id);

    public static string Allocate(IEnumerable<string>? taken, Random? random = null)
    {
        var blocked = taken?.Where(id => !string.IsNullOrWhiteSpace(id)).Select(id => id.Trim()).ToList()
            ?? [];
        blocked.AddRange(RecentlyAllocated.Keys);

        var id = NhsnOrganizationIds.Allocate(blocked, random);
        RecentlyAllocated[id] = 0;
        return id;
    }

    public static async Task<IReadOnlyCollection<string>> FacilityIdsAsync(
        IFacilityServiceClient? facilityClient,
        ILogger? logger,
        CancellationToken cancellationToken)
    {
        if (facilityClient == null)
            return [];

        try
        {
            var list = await facilityClient.GetFacilityListAsync(cancellationToken: cancellationToken);
            if (list.StatusCode == StatusCodes.Status204NoContent || list.Body == null || list.Body.Count == 0)
                return [];

            if (!list.IsSuccessStatusCode)
            {
                logger?.LogWarning(
                    "Facility list returned HTTP {StatusCode}. NHSN organization ids were checked against saved scenarios only.",
                    list.StatusCode);
                return [];
            }

            return list.Body.Keys.Where(id => !string.IsNullOrWhiteSpace(id)).Select(id => id.Trim()).ToArray();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger?.LogWarning(
                ex,
                "Facility list could not be read. NHSN organization ids were checked against saved scenarios only.");
            return [];
        }
    }
}
