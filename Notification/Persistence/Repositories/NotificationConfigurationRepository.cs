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

        public async Task<bool> AddAsync(NotificationConfig entity, CancellationToken cancellationToken = default)
        {
            await _dbContext.NotificationConfigs.AddAsync(entity, cancellationToken);            
            return _dbContext.SaveChanges() > 0;            
        }

        public async Task<bool> DeleteAsync(NotificationConfigId id, CancellationToken cancellationToken = default)
        {
            var config = await _dbContext.NotificationConfigs.FindAsync(id, cancellationToken);
            if(config is null)
            {
                return false;
            }

            _dbContext.NotificationConfigs.Remove(config);
            return _dbContext.SaveChanges() > 0;            
        }

        public async Task<NotificationConfig?> GetAsync(NotificationConfigId id, bool noTracking = false, CancellationToken cancellationToken = default)
        {
            var config = noTracking ? 
                await _dbContext.NotificationConfigs.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken) : 
                await _dbContext.NotificationConfigs.FindAsync(id, cancellationToken);
            return config;
        }

        public async Task<NotificationConfig?> GetFacilityNotificationConfigAsync(string facilityId, bool noTracking = false, CancellationToken cancellationToken = default)
        {
            var config = noTracking ? 
                await _dbContext.NotificationConfigs.AsNoTracking().FirstOrDefaultAsync(x => x.FacilityId == facilityId, cancellationToken) :
                await _dbContext.NotificationConfigs.FirstOrDefaultAsync(x => x.FacilityId == facilityId, cancellationToken);
            return config;
        }

        public async Task<(IEnumerable<NotificationConfig>, PaginationMetadata)> SearchAsync(string? searchText, string? filterFacilityBy, string? sortBy, SortOrder? sortOrder, int pageSize, int pageNumber, CancellationToken cancellationToken = default)
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

            var count = await query.CountAsync(cancellationToken);        
            query = sortOrder switch
            {
                SortOrder.Ascending => query.OrderBy(_dbContext.SetSortBy<NotificationConfig>(sortBy)),
                SortOrder.Descending => query.OrderByDescending(_dbContext.SetSortBy<NotificationConfig>(sortBy)),
                _ => query.OrderBy(x => x.CreatedOn)
            };
            
            configs = await query
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken);

            PaginationMetadata metadata = new PaginationMetadata(pageSize, pageNumber, count);

            var result = (configs, metadata);

            return result;
        }

        public async Task<bool> UpdateAsync(NotificationConfig entity, CancellationToken cancellationToken = default)
        {
            var originalEntity = await GetAsync(entity.Id, false, cancellationToken);
            if (originalEntity == null)
            {
                return false;
            }

            //check if channels were updated
            //if (originalEntity.Channels.Count != entity.Channels.Count || !originalEntity.Channels.SequenceEqual(entity.Channels))
            //{

            //}

            _dbContext.Entry(originalEntity).CurrentValues.SetValues(entity);          
            return _dbContext.SaveChanges() > 0;
        }
        public async Task<bool> ExistsAsync(NotificationConfigId id, CancellationToken cancellationToken = default)
        {
            var res = await _dbContext.NotificationConfigs.AsNoTracking().FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
            if (res != null)
                return true;
            return false;
        }
    }
}
