
namespace LantanaGroup.Link.DMRP.Business;

/// <summary>
/// The timezone a facility reports in. The host must register an implementation; the module
/// provides no default.
/// </summary>
public interface IFacilityTimeZoneSource
{
    /// <returns>The facility's timezone as recorded, or null when no facility has that id.</returns>
    Task<string?> GetTimeZoneAsync(string facilityId, CancellationToken cancellationToken = default);
}

