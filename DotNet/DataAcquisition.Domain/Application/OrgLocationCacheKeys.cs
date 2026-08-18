namespace LantanaGroup.Link.DataAcquisition.Domain.Application;

/// <summary>
/// Cache keys for organization-location data, shared between the read side
/// (<c>LocationMappingService</c>, which populates the cache) and the write side
/// (<c>OrganizationLocationConfigurationManager</c>, which invalidates it on change) so both
/// reference the same key and a condition edit takes effect immediately.
/// </summary>
public static class OrgLocationCacheKeys
{
    private const string ConditionsPrefix = "org-location-conditions:";

    /// <summary>The active org-location conditions cache key for a facility.</summary>
    public static string Conditions(string facilityId) => ConditionsPrefix + facilityId;
}
