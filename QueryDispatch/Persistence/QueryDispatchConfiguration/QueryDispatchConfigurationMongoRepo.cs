using LantanaGroup.Link.QueryDispatch.Application.Interfaces;
using LantanaGroup.Link.QueryDispatch.Domain.Entities;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace LantanaGroup.Link.QueryDispatch.Persistence.QueryDispatchConfiguration
{
    public class QueryDispatchConfigurationMongoRepo : BaseMongoRepository<QueryDispatchConfigurationEntity>, IQueryDispatchConfigurationRepository
    {
        private readonly ILogger<QueryDispatchConfigurationMongoRepo> _logger;

        public QueryDispatchConfigurationMongoRepo(IOptions<MongoConnection> mongoSettings, ILogger<QueryDispatchConfigurationMongoRepo> logger) : base(mongoSettings, logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<QueryDispatchConfigurationEntity> GetByFacilityId(string facilityId)
        {
            try
            {
                var filter = Builders<QueryDispatchConfigurationEntity>.Filter.Eq(x => x.FacilityId, facilityId);
                QueryDispatchConfigurationEntity entity = await _collection.Find(filter).FirstOrDefaultAsync();
                return entity;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to execute get QueryDispatchConfigurationEntity by id {facilityId}.", ex);
                throw new ApplicationException($"Failed to execute the request to get the specified QueryDispatchConfigurationEntity.");
            }
        }

        public async Task<bool> DeleteByFacilityId(string facilityId)
        {
            try
            {
                await _collection.DeleteOneAsync(x => x.FacilityId == facilityId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Query dispatch configuration delete exception for facility {facilityId}: {ex.Message}");
                return false;
            }
        }

        public async Task Update(QueryDispatchConfigurationEntity config)
        {
            try
            {
                await _collection.ReplaceOneAsync(x => x.Id == config.Id, config);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Update exception: {ex.Message}");
            }
        }
    }
}
