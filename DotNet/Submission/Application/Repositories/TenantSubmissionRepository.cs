using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.Repositories.Implementations;
using LantanaGroup.Link.Submission.Domain.Entities;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace LantanaGroup.Link.Submission.Application.Repositories
{
    public class TenantSubmissionRepository : MongoDbRepository<TenantSubmissionConfigEntity>
    {
        private readonly IOptions<MongoConnection> _settings;
        private readonly ILogger<MongoDbRepository<TenantSubmissionConfigEntity>> _logger;
        public TenantSubmissionRepository(IOptions<MongoConnection> mongoSettings, ILogger<MongoDbRepository<TenantSubmissionConfigEntity>> logger = null) : base(mongoSettings, logger)
        {
            _settings = mongoSettings;
            _logger = logger;
        }

        public async Task<TenantSubmissionConfigEntity> FindAsync(string facilityId, CancellationToken cancellationToken = default)
        {
            var set = await _collection.FindAsync(x => x.FacilityId == facilityId, cancellationToken: cancellationToken);
            return await set.SingleOrDefaultAsync(cancellationToken: cancellationToken);
        }

        public override async Task AddAsync(TenantSubmissionConfigEntity entity, CancellationToken cancellationToken = default)
        {
            if (cancellationToken.IsCancellationRequested)
                return;

            if (string.IsNullOrWhiteSpace(entity.Id.ToString()))
            {
                entity.Id = new TenantSubmissionConfigEntityId(Guid.NewGuid());
            }

            await _collection.InsertOneAsync(entity, cancellationToken: cancellationToken);
        }
    }
}
