using LantanaGroup.Link.Shared.Application.Models.Tenant;

namespace LantanaGroup.Link.DMRP.Business
{
    /// <summary>
    /// The facility operations that change state. The host registers an implementation performing its
    /// own persistence and job scheduling; when the DMRP module is enabled it decorates that
    /// implementation rather than replacing it, so the host's behavior still runs underneath.
    /// </summary>
    /// <remarks>
    /// Reads, ad hoc reports and report regeneration are deliberately absent: DMRP attaches no
    /// behavior to them, so routing them through this seam would add indirection with nothing behind
    /// it.
    /// </remarks>
    public interface IFacilityOperations
    {
        Task CreateAsync(FacilityModel facility, CancellationToken cancellationToken = default);

        /// <summary>
        /// Applies <paramref name="updatedFacility"/> over the facility described by
        /// <paramref name="existingFacility"/>. Both are needed because the scheduled jobs are
        /// reconciled from the difference between them.
        /// </summary>
        Task UpdateAsync(FacilityModel existingFacility, FacilityModel updatedFacility,
            CancellationToken cancellationToken = default);

        Task DeleteAsync(string facilityId, CancellationToken cancellationToken = default);

        Task SoftDeleteAsync(string facilityId, CancellationToken cancellationToken = default);

        Task RestoreAsync(FacilityModel facility, CancellationToken cancellationToken = default);
    }
}
