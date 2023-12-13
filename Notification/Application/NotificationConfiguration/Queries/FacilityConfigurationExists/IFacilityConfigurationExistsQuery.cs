namespace LantanaGroup.Link.Notification.Application.NotificationConfiguration.Queries
{
    public interface IFacilityConfigurationExistsQuery
    {
        Task<bool> Execute(string id);
    }
}
