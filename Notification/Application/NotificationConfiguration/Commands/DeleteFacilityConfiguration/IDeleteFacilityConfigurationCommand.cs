using LantanaGroup.Link.Notification.Domain.Entities;

namespace LantanaGroup.Link.Notification.Application.NotificationConfiguration.Commands
{
    public interface IDeleteFacilityConfigurationCommand
    {
        Task<bool> Execute(NotificationConfigId id, CancellationToken cancellationToken);
    }
}
