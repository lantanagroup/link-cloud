using LantanaGroup.Link.Submission.Application.Models.ApiModels;
using LantanaGroup.Link.Submission.Domain.Entities;

namespace LantanaGroup.Link.Submission.Application.Interfaces;

public interface ITenantSubmissionRepository
{
    Task<TenantSubmissionConfigEntity> FindAsync(string facilityId, CancellationToken cancellationToken = default);
    Task<TenantSubmissionConfigEntity?> GetAsync(TenantSubmissionConfigEntityId configId, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(TenantSubmissionConfigEntityId configId, CancellationToken cancellationToken = default);
    Task<bool> SaveAsync(CancellationToken cancellationToken = default);
    Task<bool> AddAsync(TenantSubmissionConfigEntity entity, CancellationToken cancellationToken = default);

}
