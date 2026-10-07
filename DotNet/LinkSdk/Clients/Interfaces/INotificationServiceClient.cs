using LantanaGroup.Link.Sdk.ApiClient;

namespace LantanaGroup.Link.Sdk.Clients;

public interface INotificationServiceClient
{
    Task<LinkApiResponse<PagedNotificationApiModel>> SearchNotificationsAsync(
        string? searchText = null,
        string? facilityId = null,
        string? notificationType = null,
        string? createdOnStart = null,
        string? createdOnEnd = null,
        string? sentOnStart = null,
        string? sentOnEnd = null,
        string? sortBy = null,
        string? sortOrder = null,
        int pageSize = 10,
        int pageNumber = 1,
        CancellationToken cancellationToken = default);

    Task<LinkApiResponse<NotificationMessageApiModel>> GetNotificationAsync(Guid id, CancellationToken cancellationToken = default);

    Task<LinkApiResponse<PagedNotificationConfigurationApiModel>> SearchConfigurationsAsync(
        string? searchText = null,
        string? facilityId = null,
        string? sortBy = null,
        string? sortOrder = null,
        int pageSize = 10,
        int pageNumber = 1,
        CancellationToken cancellationToken = default);

    Task<LinkApiResponse<NotificationConfigurationApiModel>> GetConfigurationAsync(string id, CancellationToken cancellationToken = default);

    Task<LinkApiResponse> CreateConfigurationAsync(NotificationConfigurationApiModel request, CancellationToken cancellationToken = default);

    Task<LinkApiResponse> UpdateConfigurationAsync(NotificationConfigurationApiModel request, CancellationToken cancellationToken = default);

    Task<LinkApiResponse> DeleteConfigurationAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asks the notification service to create and send one message:
    /// <c>POST /api/notification</c>.
    /// </summary>
    Task<LinkApiResponse<string>> CreateNotificationAsync(NotificationMessageApiModel message, CancellationToken cancellationToken = default);
}
