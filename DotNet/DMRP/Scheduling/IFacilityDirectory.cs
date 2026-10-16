using LantanaGroup.Link.DMRP.Business;

namespace LantanaGroup.Link.DMRP.Scheduling
{
    /// <summary>A facility as the nightly job needs to know it: its id and the zone it reports in.</summary>
    public sealed record ScheduledFacility(string FacilityId, string TimeZone);

    /// <summary>
    /// The host's facilities, read live at fire time. Facilities live in the host's own tables, so
    /// this is the module's only way to enumerate them - a sibling of <see cref="Business.IFacilityExistence"/>
    /// that also answers a single facility's timezone, which it inherits from <see cref="IFacilityTimeZoneSource"/>.
    /// </summary>
    public interface IFacilityDirectory : IFacilityTimeZoneSource
    {
        /// <summary>The distinct timezones of facilities that are not deleted. Blank zones are skipped.</summary>
        Task<IReadOnlyList<string>> GetTimeZonesAsync(CancellationToken cancellationToken = default);

        /// <summary>The facilities in one timezone that are not deleted.</summary>
        Task<IReadOnlyList<ScheduledFacility>> GetActiveInTimeZoneAsync(string timeZone,
            CancellationToken cancellationToken = default);
    }
}
