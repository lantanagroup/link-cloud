using LantanaGroup.Link.Notification.Application.Models;
using System.Security.Claims;

namespace LantanaGroup.Link.Notification.Application.Notification.Queries
{
    public interface IGetFacilityNotificatonsQuery
    {
        Task<PagedNotificationModel> Execute(ClaimsPrincipal user, string facilityId, string? sortBy, SortOrder? sortOrder, int pageSize, int pageNumber, CancellationToken cancellationToken);
    }
}
