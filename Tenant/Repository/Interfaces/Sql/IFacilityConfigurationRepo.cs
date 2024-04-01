using LantanaGroup.Link.Tenant.Entities;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace LantanaGroup.Link.Tenant.Repository.Interfaces.Sql
{
    public interface IFacilityConfigurationRepo : IPersistenceRepository<FacilityConfigModel>
    {
        public Task<FacilityConfigModel> GetAsyncByFacilityId(string facilityId, CancellationToken cancellationToken);

        public Task<List<FacilityConfigModel>> SearchAsync(string? facilityName, string? facilityId, CancellationToken cancellationToken);

        public Task<HealthCheckResult> HealthCheck(CancellationToken cancellationToken);
    }
}
