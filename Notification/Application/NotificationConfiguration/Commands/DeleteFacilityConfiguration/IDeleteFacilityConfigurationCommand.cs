namespace LantanaGroup.Link.Notification.Application.NotificationConfiguration.Commands
{
    public interface IDeleteFacilityConfigurationCommand
    {
        Task<bool> Execute(string id);
    }
}
