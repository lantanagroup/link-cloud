using LantanaGroup.Link.Shared.Application.Services;

namespace LantanaGroup.Link.DMRP.Business
{
    public interface IFacilityExistence
    {
        Task<bool> ExistsAsync(string facilityId, CancellationToken cancellationToken = default);
    }

    public sealed class TenantApiFacilityExistence : IFacilityExistence
    {
        private readonly ITenantApiService _tenantApiService;

        public TenantApiFacilityExistence(ITenantApiService tenantApiService)
        {
            _tenantApiService = tenantApiService ?? throw new ArgumentNullException(nameof(tenantApiService));
        }

        public Task<bool> ExistsAsync(string facilityId, CancellationToken cancellationToken = default) =>
            _tenantApiService.CheckFacilityExists(facilityId, cancellationToken);
    }
}
