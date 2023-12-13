namespace LantanaGroup.Link.Notification.Application.NotificationConfiguration.Commands
{ 
    public interface ICreateFacilityConfigurationCommand
    {
        Task<string> Execute(CreateFacilityConfigurationModel model);
    }
}
