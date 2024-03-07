using LantanaGroup.Link.Notification.Domain.Entities;

namespace LantanaGroup.Link.Notification.Application.NotificationConfiguration.Queries
{
    public interface IFacilityConfigurationExistsQuery
    {
        Task<bool> Execute(NotificationConfigId id, CancellationToken cancellationToken);
    }
}
