using LantanaGroup.Link.Notification.Application.Models;
using LantanaGroup.Link.Notification.Domain.Entities;

namespace LantanaGroup.Link.Notification.Application.Notification.Queries
{
    public interface IGetNotificationQuery
    {
        Task<NotificationModel> Execute(NotificationId id, CancellationToken cancellationToken);
    }
}
