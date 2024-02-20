
using LantanaGroup.Link.Notification.Application.Models;
using LantanaGroup.Link.Notification.Domain.Entities;

namespace LantanaGroup.Link.Notification.Application.Interfaces
{
    public interface INotificationRepository : IBaseRepository<NotificationEntity>
    {
        public Task<NotificationEntity?> Get(NotificationId id);
        public Task<bool> SetNotificationSentOn(NotificationId id);
        Task<(IEnumerable<NotificationEntity>, PaginationMetadata)> GetFacilityNotifications(string facilityId, string? sortBy, SortOrder? sortOrder, int pageSize, int pageNumber);
        public Task<(IEnumerable<NotificationEntity>, PaginationMetadata)> Search(string? searchText, string? filterFacilityBy, string? filterNotificationTypeBy, DateTime? createdOnStart, DateTime? createdOnEnd, DateTime? sentOnStart, DateTime? sentOnEnd, string? sortBy, SortOrder? sortOrder, int pageSize, int pageNumber);
    }
}
