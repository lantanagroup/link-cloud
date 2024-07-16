using LantanaGroup.Link.Shared.Application.Repositories.Interfaces;
using LantanaGroup.Link.Submission.Domain.Entities;

namespace LantanaGroup.Link.Submission.Application.Managers
{
    public interface ITenantSubmissionManager
    {
        Task<TenantSubmissionConfigEntity?> FindTenantSubmissionConfig(string facilityId, CancellationToken cancellationToken = default);
        Task<TenantSubmissionConfigEntity> GetTenantSubmissionConfig(string id, CancellationToken cancellationToken = default);

        Task<TenantSubmissionConfigEntity> CreateTenantSubmissionConfig(TenantSubmissionConfigEntity tenantSubmissionConfig, CancellationToken cancellationToken = default);

        Task<TenantSubmissionConfigEntity> UpdateTenantSubmissionConfig(TenantSubmissionConfigEntity tenantSubmissionConfig, CancellationToken cancellationToken = default);

        Task<bool> DeleteTenantSubmissionConfig(string configId, CancellationToken cancellationToken = default);
    }

    public class TenantSubmissionManager : ITenantSubmissionManager
    {
        private readonly IEntityRepository<TenantSubmissionConfigEntity> _queries;
        public TenantSubmissionManager(IEntityRepository<TenantSubmissionConfigEntity> queries)
        {
            _queries = queries;
        }

        public async Task<TenantSubmissionConfigEntity?> FindTenantSubmissionConfig(string facilityId, CancellationToken cancellationToken = default)
        {
            return await _queries.SingleOrDefaultAsync(c => c.FacilityId == facilityId, cancellationToken);
        }

        public async Task<TenantSubmissionConfigEntity> GetTenantSubmissionConfig(string id, CancellationToken cancellationToken = default)
        {
            return await _queries.GetAsync(id, cancellationToken);
        }

        public async Task<TenantSubmissionConfigEntity> CreateTenantSubmissionConfig(TenantSubmissionConfigEntity tenantSubmissionConfig, CancellationToken cancellationToken = default)
        {
            return await _queries.AddAsync(tenantSubmissionConfig, cancellationToken);
        }

        public async Task<TenantSubmissionConfigEntity> UpdateTenantSubmissionConfig(TenantSubmissionConfigEntity tenantSubmissionConfig, CancellationToken cancellationToken = default)
        {
            return await _queries.UpdateAsync(tenantSubmissionConfig, cancellationToken);
        }

        public async Task<bool> DeleteTenantSubmissionConfig(string facilityId, CancellationToken cancellationToken = default)
        {
            var item = await FindTenantSubmissionConfig(facilityId, cancellationToken);
           await _queries.DeleteAsync(item.Id, cancellationToken);
           return true;
        }
    }
}
