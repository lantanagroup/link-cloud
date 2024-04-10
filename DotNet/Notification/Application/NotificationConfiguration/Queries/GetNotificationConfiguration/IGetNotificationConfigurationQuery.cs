using LantanaGroup.Link.Notification.Application.Models;
using LantanaGroup.Link.Notification.Domain.Entities;

namespace LantanaGroup.Link.Notification.Application.NotificationConfiguration.Queries
{
    public interface IGetNotificationConfigurationQuery
    {
        Task<NotificationConfigurationModel> Execute(NotificationConfigId id, CancellationToken cancellationToken);
    }
}
