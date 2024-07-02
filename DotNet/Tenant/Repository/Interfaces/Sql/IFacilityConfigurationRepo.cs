using LantanaGroup.Link.Shared.Application.Repositories.Interfaces;
using LantanaGroup.Link.Tenant.Entities;
using LantanaGroup.Link.Tenant.Models;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace LantanaGroup.Link.Tenant.Repository.Interfaces.Sql
{
    public interface IFacilityConfigurationRepo : IEntityRepository<FacilityConfigModel>
    {
        public Task<List<FacilityConfigModel>> GetAsync(CancellationToken cancellationToken);

        public Task<FacilityConfigModel> GetAsyncByFacilityId(string facilityId, CancellationToken cancellationToken);

        public Task<FacilityConfigModel> GetAsyncById(Guid id, CancellationToken cancellationToken);

        public Task<bool> RemoveAsync(Guid id, CancellationToken cancellationToken);

        public Task<(List<FacilityConfigModel>, PaginationMetadata)> SearchAsync(string? facilityName, string? facilityId, string? sortBy, SortOrder? sortOrder, int pageSize, int pageNumber, CancellationToken cancellationToken);

        public Task<HealthCheckResult> HealthCheck(CancellationToken cancellationToken);
    }
}
