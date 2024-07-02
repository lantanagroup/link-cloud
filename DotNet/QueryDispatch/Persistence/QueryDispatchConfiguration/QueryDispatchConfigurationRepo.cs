using LantanaGroup.Link.QueryDispatch.Application.Interfaces;
using LantanaGroup.Link.QueryDispatch.Domain.Entities;
using LantanaGroup.Link.Shared.Application.Repositories.Implementations;
using Microsoft.EntityFrameworkCore;
using MongoDB.Driver;
using QueryDispatch.Domain.Context;

namespace LantanaGroup.Link.QueryDispatch.Persistence.QueryDispatchConfiguration
{
    public class QueryDispatchConfigurationRepo : EntityRepository<QueryDispatchConfigurationEntity>, IQueryDispatchConfigurationRepository
    {
        private readonly ILogger<QueryDispatchConfigurationRepo> _logger;
        private readonly QueryDispatchDbContext _dbContext;

        public QueryDispatchConfigurationRepo(ILogger<QueryDispatchConfigurationRepo> logger, QueryDispatchDbContext dbContext) : base(logger, dbContext)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        }

        public async Task<QueryDispatchConfigurationEntity> GetByFacilityId(string facilityId)
        {
            try
            {
                var entity = await _dbContext.QueryDispatchConfigurations.FirstOrDefaultAsync(x => x.FacilityId == facilityId);
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
                var entity = await _dbContext.QueryDispatchConfigurations.FirstOrDefaultAsync(x => x.FacilityId == facilityId);
                if (entity != null)
                {
                    _dbContext.QueryDispatchConfigurations.Remove(entity);
                    await _dbContext.SaveChangesAsync();
                }
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
                _dbContext.QueryDispatchConfigurations.Update(config);
                await _dbContext.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError($"Update exception: {ex.Message}");
            }
        }

        public async Task<List<QueryDispatchConfigurationEntity>> GetAllAsync()
        {
            try
            {
                var entities = await _dbContext.QueryDispatchConfigurations.ToListAsync();
                return entities;
            }
            catch(Exception ex)
            {
                _logger.LogError($"Failed to get all query dispatch configurations.", ex);
                throw new ApplicationException($"Failed to query all dispatch configurations.");
            }
        }
    }
}
