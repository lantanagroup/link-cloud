using LantanaGroup.Link.Submission.Application.Interfaces;
using LantanaGroup.Link.Submission.Application.Models.ApiModels;

namespace LantanaGroup.Link.Submission.Application.Managers
{
    public class TenantSubmissionManager : ITenantSubmissionManager
    {
        private readonly ITenantSubmissionQueries _queries;
        public TenantSubmissionManager(ITenantSubmissionQueries queries)
        {
            _queries = queries;
        }

        public async Task<TenantSubmissionConfig> FindTenantSubmissionConfig(string facilityId, CancellationToken cancellationToken = default)
        {
            return await _queries.FindTenantSubmissionConfig(facilityId, cancellationToken);
        }

        public async Task<TenantSubmissionConfig> GetTenantSubmissionConfig(string id, CancellationToken cancellationToken = default)
        {
            return await _queries.GetTenantSubmissionConfig(id, cancellationToken);
        }

        public async Task<TenantSubmissionConfig> CreateTenantSubmissionConfig(TenantSubmissionConfig tenantSubmissionConfig, CancellationToken cancellationToken = default)
        {
            return await _queries.CreateTenantSubmissionConfig(tenantSubmissionConfig, cancellationToken);
        }

        public async Task<TenantSubmissionConfig> UpdateTenantSubmissionConfig(TenantSubmissionConfig tenantSubmissionConfig, CancellationToken cancellationToken = default)
        {
            return await _queries.UpdateTenantSubmissionConfig( tenantSubmissionConfig, cancellationToken);
        }

        public async Task<bool> DeleteTenantSubmissionConfig(string facilityId, CancellationToken cancellationToken = default)
        {
           return await _queries.DeleteTenantSubmissionConfig(facilityId, cancellationToken);
        }
    }
}
