using LantanaGroup.Link.Notification.Application.Models;

namespace LantanaGroup.Link.Notification.Application.NotificationConfiguration.Commands
{ 
    public interface ICreateFacilityConfigurationCommand
    {
        Task<NotificationConfigurationModel> Execute(CreateFacilityConfigurationModel model, CancellationToken cancellationToken);
    }
}
