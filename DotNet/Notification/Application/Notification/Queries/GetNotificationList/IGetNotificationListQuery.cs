using LantanaGroup.Link.Notification.Application.Models;

namespace LantanaGroup.Link.Notification.Application.Notification.Queries
{
    public interface IGetNotificationListQuery
    {
        Task<PagedNotificationModel> Execute(string? searchText, string? filterFacilityBy, string? filterNotificationTypeBy, DateTime? createdOnStart, DateTime? createdOnEnd, DateTime? sentOnStart, DateTime? sentOnEnd, string? sortBy, SortOrder? sortOrder, int pageSize, int pageNumber, CancellationToken cancellationToken);
    }
}
