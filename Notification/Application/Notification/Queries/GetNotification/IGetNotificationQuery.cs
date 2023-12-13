using LantanaGroup.Link.Notification.Application.Models;

namespace LantanaGroup.Link.Notification.Application.Notification.Queries
{
    public interface IGetNotificationQuery
    {
        Task<NotificationModel> Execute(string id);
    }
}
