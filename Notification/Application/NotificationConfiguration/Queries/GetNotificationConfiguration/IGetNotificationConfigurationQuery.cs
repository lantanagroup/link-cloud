using LantanaGroup.Link.Notification.Application.Models;

namespace LantanaGroup.Link.Notification.Application.NotificationConfiguration.Queries
{
    public interface IGetNotificationConfigurationQuery
    {
        Task<NotificationConfigurationModel> Execute(string id);
    }
}
