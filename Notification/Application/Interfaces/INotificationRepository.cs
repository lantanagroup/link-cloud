
using LantanaGroup.Link.Notification.Application.Models;
using LantanaGroup.Link.Notification.Domain.Entities;

namespace LantanaGroup.Link.Notification.Application.Interfaces
{
    public interface INotificationRepository : IBaseRepository<NotificationEntity>
    {
        public Task<NotificationEntity?> GetAsync(NotificationId id, bool noTracking = false, CancellationToken cancellationToken = default);
        public Task<bool> SetNotificationSentOnAsync(NotificationId id, CancellationToken cancellationToken = default);
        Task<(IEnumerable<NotificationEntity>, PaginationMetadata)> GetFacilityNotificationsAsync(string facilityId, string? sortBy, SortOrder? sortOrder, int pageSize, int pageNumber, CancellationToken cancellationToken = default);
        public Task<(IEnumerable<NotificationEntity>, PaginationMetadata)> SearchAsync(string? searchText, string? filterFacilityBy, string? filterNotificationTypeBy, DateTime? createdOnStart, DateTime? createdOnEnd, DateTime? sentOnStart, DateTime? sentOnEnd, string? sortBy, SortOrder? sortOrder, int pageSize, int pageNumber, CancellationToken cancellationToken = default);
    }
}
