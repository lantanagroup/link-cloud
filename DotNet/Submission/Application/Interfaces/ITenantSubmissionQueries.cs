using LantanaGroup.Link.Submission.Application.Models.ApiModels;

namespace LantanaGroup.Link.Submission.Application.Interfaces
{
    public interface ITenantSubmissionQueries
    {
        public Task<TenantSubmissionConfig> FindTenantSubmissionConfig(string facilityId, CancellationToken cancellationToken = default);

        public Task<TenantSubmissionConfig> GetTenantSubmissionConfig(string facilityId, CancellationToken cancellationToken = default);

        public Task<TenantSubmissionConfig> CreateTenantSubmissionConfig(TenantSubmissionConfig tenantSubmissionConfig, CancellationToken cancellationToken = default);

        public Task<TenantSubmissionConfig> UpdateTenantSubmissionConfig(TenantSubmissionConfig tenantSubmissionConfig, CancellationToken cancellationToken = default);

        public Task<bool> DeleteTenantSubmissionConfig(string facilityId, CancellationToken cancellationToken = default);
    }
}
