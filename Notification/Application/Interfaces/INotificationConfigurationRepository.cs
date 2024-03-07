using LantanaGroup.Link.Notification.Application.Models;
using LantanaGroup.Link.Notification.Domain.Entities;

namespace LantanaGroup.Link.Notification.Application.Interfaces
{
    public interface INotificationConfigurationRepository : IBaseRepository<NotificationConfig>
    {   
        public Task<NotificationConfig?> GetAsync(NotificationConfigId id, bool noTracking = false, CancellationToken cancellationToken = default);
        public Task<NotificationConfig?> GetFacilityNotificationConfigAsync(string facilityId, bool noTracking = false, CancellationToken cancellationToken = default);
        public Task<(IEnumerable<NotificationConfig>, PaginationMetadata)> SearchAsync(string? searchText, string? filterFacilityBy, string? sortBy, SortOrder? sortOrder, int pageSize, int pageNumber, CancellationToken cancellationToken = default);
        public Task<bool> DeleteAsync(NotificationConfigId id, CancellationToken cancellationToken = default);
        Task<bool> ExistsAsync(NotificationConfigId id, CancellationToken cancellationToken = default);

    }
}
