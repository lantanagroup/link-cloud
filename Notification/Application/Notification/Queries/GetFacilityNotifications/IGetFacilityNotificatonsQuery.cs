using LantanaGroup.Link.Notification.Application.Models;

namespace LantanaGroup.Link.Notification.Application.Notification.Queries
{
    public interface IGetFacilityNotificatonsQuery
    {
        Task<PagedNotificationModel> Execute(string facilityId, string? sortBy, SortOrder? sortOrder, int pageSize, int pageNumber, CancellationToken cancellationToken);
    }
}
