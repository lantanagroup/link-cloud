using LantanaGroup.Link.DMRP.Scheduling;
using LantanaGroup.Link.Shared.Domain.Repositories.Interfaces;
using LantanaGroup.Link.Tenant.Entities;

namespace LantanaGroup.Link.Tenant.Business
{
    /// <summary>
    /// The host's facilities, read from the Tenant service's own database, which is where the host
    /// persists them. The module needs this both to resolve a single facility's reporting period -
    /// the month the facility is in by its own timezone, not UTC - and, for the nightly job, to
    /// enumerate every facility due to fire, grouped by timezone.
    /// </summary>
    public sealed class TenantFacilityDirectory : IFacilityDirectory
    {
        private readonly IEntityRepository<Facility> _facilities;

        public TenantFacilityDirectory(IEntityRepository<Facility> facilities)
        {
            _facilities = facilities ?? throw new ArgumentNullException(nameof(facilities));
        }

        /// <returns>The facility's timezone as recorded, or null when no non-deleted facility has that id.</returns>
        public async Task<string?> GetTimeZoneAsync(string facilityId, CancellationToken cancellationToken = default)
        {
            var facility = await _facilities.FirstOrDefaultAsync(
                f => f.FacilityId == facilityId && !f.IsDeleted, cancellationToken);

            return facility?.TimeZone;
        }

        public async Task<IReadOnlyList<string>> GetTimeZonesAsync(CancellationToken cancellationToken = default)
        {
            var active = await _facilities.FindAsync(f => !f.IsDeleted && f.TimeZone != "", cancellationToken);

            return active.Select(f => f.TimeZone).Distinct(StringComparer.Ordinal).OrderBy(z => z).ToList();
        }

        public async Task<IReadOnlyList<ScheduledFacility>> GetActiveInTimeZoneAsync(string timeZone,
            CancellationToken cancellationToken = default)
        {
            var facilities = await _facilities.FindAsync(f => !f.IsDeleted && f.TimeZone == timeZone, cancellationToken);

            return facilities.Select(f => new ScheduledFacility(f.FacilityId, f.TimeZone)).ToList();
        }
    }
}
