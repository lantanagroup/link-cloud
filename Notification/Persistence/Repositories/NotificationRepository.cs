using LantanaGroup.Link.Notification.Application.Interfaces;
using LantanaGroup.Link.Notification.Application.Models;
using LantanaGroup.Link.Notification.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace LantanaGroup.Link.Notification.Persistence.Repositories
{
    public class NotificationRepository : INotificationRepository
    {
        private readonly ILogger<NotificationRepository> _logger;
        private readonly NotificationDbContext _dbContext;

        public NotificationRepository(ILogger<NotificationRepository> logger, NotificationDbContext dbContext)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        }

        public async Task<bool> AddAsync(NotificationEntity entity, CancellationToken cancellationToken = default)
        {
            await _dbContext.Notifications.AddAsync(entity, cancellationToken);  
            return await _dbContext.SaveChangesAsync(cancellationToken) > 0;
        }

        public async Task<NotificationEntity?> GetAsync(NotificationId id, bool noTracking = false, CancellationToken cancellationToken = default)
        {
            var notification = noTracking ? 
                await _dbContext.Notifications.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken) :
                await _dbContext.Notifications.FindAsync(id, cancellationToken);
            return notification;
        }

        public async Task<(IEnumerable<NotificationEntity>, PaginationMetadata)> GetFacilityNotificationsAsync(string facilityId, string? sortBy, SortOrder? sortOrder, int pageSize, int pageNumber, CancellationToken cancellationToken = default)
        {
            IEnumerable<NotificationEntity> notifications;
            var query = _dbContext.Notifications.AsNoTracking().Where(x => x.FacilityId == facilityId).AsQueryable();
            var count = await query.CountAsync(cancellationToken);

            sortBy ??= "CreatedOn";
            query = sortOrder switch
            {
                SortOrder.Ascending => sortBy.Equals("SentOn") ? query.OrderBy(x => x.SentOn.Max()) : query.OrderBy(SetSortBy<NotificationEntity>(sortBy)),
                SortOrder.Descending => sortBy.Equals("SentOn") ? query.OrderByDescending(x => x.SentOn.Max()) : query.OrderByDescending(SetSortBy<NotificationEntity>(sortBy)),
                _ => query.OrderBy(x => x.CreatedOn)
            };

            notifications = await query
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken);

            PaginationMetadata metadata = new PaginationMetadata(pageSize, pageNumber, count);

            var result = (notifications, metadata);

            return result;
        }

        public async Task<(IEnumerable<NotificationEntity>, PaginationMetadata)> SearchAsync(string? searchText, string? filterFacilityBy, string? filterNotificationTypeBy, DateTime? createdOnStart, DateTime? createdOnEnd, DateTime? sentOnStart, DateTime? sentOnEnd, string? sortBy, SortOrder? sortOrder, int pageSize, int pageNumber, CancellationToken cancellationToken = default)
        {
            IEnumerable<NotificationEntity> notifications;
            var query = _dbContext.Notifications.AsNoTracking().AsQueryable();

            #region Build Query
            if (searchText is not null && searchText.Length > 0)
            {
                query = query.Where(x =>
                    (x.FacilityId != null && x.FacilityId.Contains(searchText)) ||
                    (x.NotificationType != null && x.NotificationType.Contains(searchText)) ||
                    (x.Subject != null && x.Subject.Contains(searchText)) ||
                    (x.Body != null && x.Body.Contains(searchText)) ||
                    (x.Recipients != null && x.Recipients.Contains(searchText)));                                                                                                                          
            }

            if (!string.IsNullOrEmpty(filterFacilityBy))
            {
                query = query.Where(x => x.FacilityId == filterFacilityBy);
            }

            if (!string.IsNullOrEmpty(filterNotificationTypeBy))
            {
                query = query.Where(x => x.NotificationType == filterNotificationTypeBy);
            }

            if (createdOnStart is not null)
            {
                query = query.Where(x => x.CreatedOn >= createdOnStart);
            }

            if (createdOnEnd is not null)
            {
                query = query.Where(x => x.CreatedOn <= createdOnEnd);
            }

            if (sentOnStart is not null)
            {
                query = query.Where(x => x.SentOn.Any(y => y >= sentOnStart));
            }

            if (sentOnEnd is not null)
            {
                query = query.Where(x => x.SentOn.Any(y => y <= sentOnEnd));
            }

            #endregion

            var count = await query.CountAsync(cancellationToken);

            sortBy ??= "CreatedOn";
            
            query = sortOrder switch
            {
                SortOrder.Ascending => sortBy.Equals("SentOn") ? query.OrderBy(x => x.SentOn.Max()) : query.OrderBy(SetSortBy<NotificationEntity>(sortBy)),
                SortOrder.Descending => sortBy.Equals("SentOn") ? query.OrderByDescending(x => x.SentOn.Max()) :  query.OrderByDescending(SetSortBy<NotificationEntity>(sortBy)),
                _ => query.OrderBy(x => x.CreatedOn)
            };

            notifications = await query                
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken);

            PaginationMetadata metadata = new PaginationMetadata(pageSize, pageNumber, count);

            var result = (notifications, metadata);
            return result;
        }        

        public async Task<bool> SetNotificationSentOnAsync(NotificationId id, CancellationToken cancellationToken = default)
        {
            var notification = await _dbContext.Notifications.FindAsync(id, cancellationToken);
            if(notification is null)
            {
                return false;
            }

            notification.SentOn.Add(DateTime.UtcNow);
            return await _dbContext.SaveChangesAsync(cancellationToken) > 0;
        }

        public async Task<bool> UpdateAsync(NotificationEntity entity, CancellationToken cancellationToken = default)
        {
            var originalEntity = await GetAsync(entity.Id, false, cancellationToken);
            if (originalEntity == null)
            {
                return false;
            }

            _dbContext.Entry(originalEntity).CurrentValues.SetValues(entity);
            return await _dbContext.SaveChangesAsync(cancellationToken) > 0;
        }

        /// <summary>
        /// Creates a sort expression for the given sortBy parameter
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="sortBy"></param>
        /// <returns></returns>
        private Expression<Func<T, object>> SetSortBy<T>(string? sortBy)
        {
            var sortKey = sortBy switch
            {
                "FacilityId" => "FacilityId",
                "NotificationType" => "NotificationType",
                "CreatedOn" => "CreatedOn",
                "LastModifiedOn" => "LastModifiedOn",
                _ => "CreatedOn"
            };

            var parameter = Expression.Parameter(typeof(T), "p");
            var sortExpression = Expression.Lambda<Func<T, object>>(Expression.Convert(Expression.Property(parameter, sortKey), typeof(object)), parameter);

            return sortExpression;
        }
    }
}
