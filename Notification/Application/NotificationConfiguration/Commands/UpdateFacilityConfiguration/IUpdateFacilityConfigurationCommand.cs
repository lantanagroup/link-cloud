
namespace LantanaGroup.Link.Notification.Application.NotificationConfiguration.Commands
{
    public interface IUpdateFacilityConfigurationCommand
    {
        Task<string> Execute(UpdateFacilityConfigurationModel model);
    }
}
