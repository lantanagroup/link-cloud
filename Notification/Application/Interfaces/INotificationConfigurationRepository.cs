using LantanaGroup.Link.Notification.Application.Models;
using LantanaGroup.Link.Notification.Domain.Entities;

namespace LantanaGroup.Link.Notification.Application.Interfaces
{
    public interface INotificationConfigurationRepository : IBaseRepository<NotificationConfig>
    {
        public bool Update(NotificationConfig config);
        public Task<bool> UpdateAsync(NotificationConfig config);
        public Task<NotificationConfig> GetNotificationConfigByFacilityAsync(string facilityId);
        public (IEnumerable<NotificationConfig>, PaginationMetadata) Find(string? searchText, string? filterFacilityBy, string? sortBy, int pageSize, int pageNumber);
        public Task<(IEnumerable<NotificationConfig>, PaginationMetadata)> FindAsync(string? searchText, string? filterFacilityBy, string? sortBy, int pageSize, int pageNumber);
        public Task<bool> DeleteAsync(string id);

    }
}
