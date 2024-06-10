using Amazon.Runtime.Internal;
using LantanaGroup.Link.Submission.Domain.Entities;
using LantanaGroup.Link.Submission.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Linq.Expressions;

namespace LantanaGroup.Link.Submission.Application.Repositories
{
    public class TenantSubmissionRepository
    {
        private readonly TenantSubmissionDbContext _dbContext;
        private readonly ILogger<TenantSubmissionConfigEntity> _logger;
        public TenantSubmissionRepository(ILogger<TenantSubmissionConfigEntity> logger, TenantSubmissionDbContext dbContext)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _logger = logger;
        }

        public async Task<TenantSubmissionConfigEntity> FindAsync(string facilityId, CancellationToken cancellationToken = default)
        {
            return await _dbContext.TenantSubmissionConfigEntities.FirstOrDefaultAsync(x => x.FacilityId == facilityId, cancellationToken);
        }

        public async Task<TenantSubmissionConfigEntity?> GetAsync(TenantSubmissionConfigEntityId configId, CancellationToken cancellationToken = default)
        {
            return await _dbContext.TenantSubmissionConfigEntities.FirstOrDefaultAsync(x => x.Id == configId, cancellationToken);
        }

        public async Task<bool> AddAsync(TenantSubmissionConfigEntity entity, CancellationToken cancellationToken = default)
        {
            if (cancellationToken.IsCancellationRequested)
                return false;

            if (string.IsNullOrWhiteSpace(entity.Id.ToString()))
            {
                entity.Id = new TenantSubmissionConfigEntityId(Guid.NewGuid());
            }

            await _dbContext.TenantSubmissionConfigEntities.AddAsync(entity, cancellationToken);
            return await SaveAsync(cancellationToken);
        }

        public async Task<bool> SaveAsync(CancellationToken cancellationToken = default)
        {
            return await _dbContext.SaveChangesAsync(cancellationToken) > 0;
        }


        public async Task<bool> DeleteAsync(TenantSubmissionConfigEntityId configId, CancellationToken cancellationToken = default)
        {
            return await _dbContext.TenantSubmissionConfigEntities
                .Where(x => x.Id == configId)
                .ExecuteDeleteAsync(cancellationToken) >= 0;
        }
    }
}
