using LantanaGroup.Link.Notification.Application.Interfaces;
using LantanaGroup.Link.Notification.Application.Models;
using LantanaGroup.Link.Notification.Domain.Entities;
using Microsoft.EntityFrameworkCore;

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

        public Task<(IEnumerable<NotificationConfig>, PaginationMetadata)> Search(string? searchText, string? filterFacilityBy, string? sortBy, SortOrder? sortOrder, int pageSize, int pageNumber)
        {
            IEnumerable<NotificationConfig> configs;
            var query = _dbContext.NotificationConfigs.AsNoTracking().AsQueryable();

            #region Build Query
            if (searchText is not null && searchText.Length > 0)
            {
                query = query.Where(x =>
                    (x.FacilityId != null && x.FacilityId.Contains(searchText)) ||
                    (x.EmailAddresses != null && x.EmailAddresses.Contains(searchText))); //||
                    //(x.EnabledNotifications != null && x.EnabledNotifications.Any(y => y.NotificationType.Contains(searchText))) ||
                    //(x.Channels != null && x.Channels.Any(y => y.Name.Contains(searchText))));
            }

            if (filterFacilityBy is not null && filterFacilityBy.Length > 0)
            {
                query = query.Where(x => x.FacilityId == filterFacilityBy);
            }
            #endregion

            var count = query.Count();        
            query = sortOrder switch
            {
                SortOrder.Ascending => query.OrderBy(_dbContext.SetSortBy<NotificationConfig>(sortBy)),
                SortOrder.Descending => query.OrderByDescending(_dbContext.SetSortBy<NotificationConfig>(sortBy)),
                _ => query.OrderBy(x => x.CreatedOn)
            };
            
            configs = query
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            PaginationMetadata metadata = new PaginationMetadata(pageSize, pageNumber, count);

            return Task.FromResult<(IEnumerable<NotificationConfig>, PaginationMetadata)>((configs, metadata));
        }

        public Task<bool> Update(NotificationConfig entity)
        {
            _dbContext.NotificationConfigs.Update(entity);
            return Task.FromResult(_dbContext.SaveChanges() > 0);
        }
    }
}
