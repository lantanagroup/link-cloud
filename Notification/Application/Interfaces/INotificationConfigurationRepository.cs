using LantanaGroup.Link.Notification.Application.Models;
using LantanaGroup.Link.Notification.Domain.Entities;

namespace LantanaGroup.Link.Notification.Application.Interfaces
{
    public interface INotificationConfigurationRepository : IBaseRepository<NotificationConfig>
    {   
        public Task<NotificationConfig?> Get(NotificationConfigId id);
        public Task<NotificationConfig?> GetFacilityNotificationConfig(string facilityId);
        public Task<(IEnumerable<NotificationConfig>, PaginationMetadata)> Search(string? searchText, string? filterFacilityBy, string? sortBy, SortOrder? sortOrder, int pageSize, int pageNumber);
        public Task<bool> Delete(NotificationConfigId id);

    }
}
