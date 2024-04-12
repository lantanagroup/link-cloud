using LantanaGroup.Link.Notification.Application.Models;

namespace LantanaGroup.Link.Notification.Application.NotificationConfiguration.Queries
{
    public interface IGetFacilityConfigurationQuery
    {
        Task<NotificationConfigurationModel> Execute(string facilityId, CancellationToken cancellationToken);
    }
}
