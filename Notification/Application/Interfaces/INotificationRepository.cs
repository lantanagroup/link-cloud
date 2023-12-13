using LantanaGroup.Link.Notification.Application.Models;
using LantanaGroup.Link.Notification.Domain.Entities;

namespace LantanaGroup.Link.Notification.Application.Interfaces
{
    public interface INotificationRepository : IBaseRepository<NotificationEntity>
    {
        public Task<bool> SetNotificationSentOn(string id);
        public (IEnumerable<NotificationEntity>, PaginationMetadata) Find(string? searchText, string? filterFacilityBy, string? filterNotificationTypeBy, DateTime? createdOnStart, DateTime? createdOnEnd, DateTime? sentOnStart, DateTime? sentOnEnd, string? sortBy, int pageSize, int pageNumber);
        public Task<(IEnumerable<NotificationEntity>, PaginationMetadata)> FindAsync(string? searchText, string? filterFacilityBy, string? filterNotificationTypeBy, DateTime? createdOnStart, DateTime? createdOnEnd, DateTime? sentOnStart, DateTime? sentOnEnd, string? sortBy, int pageSize, int pageNumber);
    }
}
