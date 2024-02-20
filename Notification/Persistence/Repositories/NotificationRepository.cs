﻿using LantanaGroup.Link.Notification.Application.Interfaces;
using LantanaGroup.Link.Notification.Application.Models;
using LantanaGroup.Link.Notification.Domain.Entities;
using Microsoft.EntityFrameworkCore;

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

        public Task<bool> Add(NotificationEntity entity)
        {
            _dbContext.Notifications.Add(entity);  
            return Task.FromResult(_dbContext.SaveChanges() > 0);
        }

        public Task<NotificationEntity?> Get(NotificationId id)
        {
            var notification = _dbContext.Notifications.Find(id);
            return Task.FromResult(notification);
        }

        public Task<(IEnumerable<NotificationEntity>, PaginationMetadata)> Search(string? searchText, string? filterFacilityBy, string? filterNotificationTypeBy, DateTime? createdOnStart, DateTime? createdOnEnd, DateTime? sentOnStart, DateTime? sentOnEnd, string? sortBy, SortOrder? sortOrder, int pageSize, int pageNumber)
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
                    (x.Body != null && x.Body.Contains(searchText)));                                                                                                                           
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

            var count = query.Count();
            query = _dbContext.SetSortBy(query, sortBy, sortOrder.Equals(SortOrder.Ascending));
            notifications = query                
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            PaginationMetadata metadata = new PaginationMetadata(pageSize, pageNumber, count);

            return Task.FromResult<(IEnumerable<NotificationEntity>, PaginationMetadata)>((notifications, metadata));
        }        

        public Task<bool> SetNotificationSentOn(NotificationId id)
        {
            var notification = _dbContext.Notifications.Find(id);
            if(notification is null)
            {
                return Task.FromResult(false);
            }

            notification.SentOn.Add(DateTime.UtcNow);
            return Task.FromResult(_dbContext.SaveChanges() > 0);
        }

        public Task<bool> Update(NotificationEntity entity)
        {
            _dbContext.Notifications.Update(entity);
            return Task.FromResult(_dbContext.SaveChanges() > 0);
        }        
    }
}
