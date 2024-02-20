using LantanaGroup.Link.Notification.Application.Interfaces;
using LantanaGroup.Link.Notification.Application.Models;
using LantanaGroup.Link.Notification.Domain.Entities;

namespace LantanaGroup.Link.Notification.Persistence.Repositories
{
    public class NotificationConfigurationRepository : INotificationConfigurationRepository
    {
        private readonly ILogger<NotificationConfigurationRepository> _logger;
        private readonly NotificationDbContext _dbContext;

        public NotificationConfigurationRepository(ILogger<NotificationConfigurationRepository> logger, NotificationDbContext dbContext)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        }

        public Task<bool> Add(NotificationConfig entity)
        {
            _dbContext.NotificationConfigs.Add(entity);            
            return Task.FromResult(_dbContext.SaveChanges() > 0);            
        }

        public Task<bool> Delete(NotificationConfigId id)
        {
            var config = _dbContext.NotificationConfigs.Find(id);
            if(config is null)
            {
                return Task.FromResult(false);
            }

            _dbContext.NotificationConfigs.Remove(config);
            return Task.FromResult(_dbContext.SaveChanges() > 0);            
        }

        public Task<NotificationConfig?> Get(NotificationConfigId id)
        {
            var config = _dbContext.NotificationConfigs.Find(id);
            return Task.FromResult(config);
        }

        public Task<NotificationConfig?> GetFacilityNotificationConfig(string facilityId)
        {
            var config = _dbContext.NotificationConfigs.FirstOrDefault(x => x.FacilityId == facilityId);
            return Task.FromResult(config);
        }

        public (IEnumerable<NotificationConfig>, PaginationMetadata) Search(string? searchText, string? filterFacilityBy, string? sortBy, int pageSize, int pageNumber)
        {
            throw new NotImplementedException();
        }

        public Task<bool> Update(NotificationConfig entity)
        {
            _dbContext.NotificationConfigs.Update(entity);
            return Task.FromResult(_dbContext.SaveChanges() > 0);
        }
    }
}
