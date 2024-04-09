
namespace LantanaGroup.Link.Notification.Application.NotificationConfiguration.Commands
{
    public interface IUpdateFacilityConfigurationCommand
    {
        Task<bool> Execute(UpdateFacilityConfigurationModel model, CancellationToken cancellationToken);
    }
}
