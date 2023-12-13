using LantanaGroup.Link.DemoApiGateway.Application.models.notification;

namespace LantanaGroup.Link.DemoApiGateway.Services.Client
{
    public interface INotificationService
    {
        Task<HttpResponseMessage> ListNotifications(string? searchText, string? filterFacilityBy, string? filterNotificationTypeBy, DateTime? createdOnStart, DateTime? createdOnEnd, DateTime? sentOnStart, DateTime? sentOnEnd, string? sortBy, int pageSize = 10, int pageNumber = 1);
        Task<HttpResponseMessage> CreateNotification(NotificationMessage model);
        Task<HttpResponseMessage> ListConfigurations(string? searchText, string? filterFacilityBy, string? sortBy, int pageSize = 10, int pageNumber = 1);
        Task<HttpResponseMessage> CreateNotificationConfiguration(NotificationConfigurationModel model);
        Task<HttpResponseMessage> UpdateNotificationConfiguration(NotificationConfigurationModel model);
        Task<HttpResponseMessage> DeleteNotificationConfiguration(Guid id);
    }
}
