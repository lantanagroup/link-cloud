using Flurl.Http;
using LantanaGroup.Link.Sdk.ApiClient;
using LantanaGroup.Link.Shared.Application.Extensions.Security;
using LantanaGroup.Link.Shared.Application.Interfaces.Services.Security.Token;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using Microsoft.Extensions.Options;

namespace LantanaGroup.Link.Sdk.Clients;

public class NotificationServiceClient : LinkApiClientBase, INotificationServiceClient
{
    public NotificationServiceClient(
        IOptions<ServiceRegistry> serviceRegistry,
        IOptions<BackendAuthenticationServiceExtension.LinkBearerServiceOptions> bearerOptions,
        IOptions<LinkTokenServiceSettings> tokenServiceSettings,
        ICreateSystemToken tokenService)
        : base(
            serviceRegistry.Value.NotificationServiceApiUrl
                ?? throw new InvalidOperationException("Notification service URL is not configured in ServiceRegistry."),
            bearerOptions, tokenServiceSettings, tokenService)
    { }

    public Task<LinkApiResponse<PagedNotificationApiModel>> SearchNotificationsAsync(
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
        CancellationToken cancellationToken = default) =>
        SendAsync<PagedNotificationApiModel>(() =>
        {
            var request = Request("notification")
                .SetQueryParam("pageSize", pageSize)
                .SetQueryParam("pageNumber", pageNumber);
            request = Set(request, "searchText", searchText);
            request = Set(request, "filterFacilityBy", facilityId);
            request = Set(request, "filterNotificationTypeBy", notificationType);
            request = Set(request, "createdOnStart", createdOnStart);
            request = Set(request, "createdOnEnd", createdOnEnd);
            request = Set(request, "sentOnStart", sentOnStart);
            request = Set(request, "sentOnEnd", sentOnEnd);
            request = Set(request, "sortBy", sortBy);
            request = Set(request, "sortOrder", sortOrder);
            return request.GetAsync(cancellationToken: cancellationToken);
        });

    public Task<LinkApiResponse<NotificationMessageApiModel>> GetNotificationAsync(
        Guid id,
        CancellationToken cancellationToken = default) =>
        SendAsync<NotificationMessageApiModel>(() => Request($"notification/{id}")
            .GetAsync(cancellationToken: cancellationToken));

    public Task<LinkApiResponse<PagedNotificationConfigurationApiModel>> SearchConfigurationsAsync(
        string? searchText = null,
        string? facilityId = null,
        string? sortBy = null,
        string? sortOrder = null,
        int pageSize = 10,
        int pageNumber = 1,
        CancellationToken cancellationToken = default) =>
        SendAsync<PagedNotificationConfigurationApiModel>(() =>
        {
            var request = Request("notification/configuration")
                .SetQueryParam("pageSize", pageSize)
                .SetQueryParam("pageNumber", pageNumber);
            request = Set(request, "searchText", searchText);
            request = Set(request, "filterFacilityBy", facilityId);
            request = Set(request, "sortBy", sortBy);
            request = Set(request, "sortOrder", sortOrder);
            return request.GetAsync(cancellationToken: cancellationToken);
        });

    public Task<LinkApiResponse<NotificationConfigurationApiModel>> GetConfigurationAsync(
        string id,
        CancellationToken cancellationToken = default) =>
        SendAsync<NotificationConfigurationApiModel>(() => Request($"notification/configuration/{id}")
            .GetAsync(cancellationToken: cancellationToken));

    public Task<LinkApiResponse> CreateConfigurationAsync(
        NotificationConfigurationApiModel request,
        CancellationToken cancellationToken = default) =>
        SendAsync(() => Request("notification/configuration")
            .PostJsonAsync(request, cancellationToken: cancellationToken));

    public Task<LinkApiResponse> UpdateConfigurationAsync(
        NotificationConfigurationApiModel request,
        CancellationToken cancellationToken = default) =>
        SendAsync(() => Request("notification/configuration")
            .PutJsonAsync(request, cancellationToken: cancellationToken));

    public Task<LinkApiResponse> DeleteConfigurationAsync(
        string id,
        CancellationToken cancellationToken = default) =>
        SendAsync(() => Request($"notification/configuration/{id}")
            .DeleteAsync(cancellationToken: cancellationToken));

    private static IFlurlRequest Set(IFlurlRequest request, string name, string? value) =>
        string.IsNullOrWhiteSpace(value) ? request : request.SetQueryParam(name, value);
}
