using LantanaGroup.Link.DemoApiGateway.Application.models;
using LantanaGroup.Link.DemoApiGateway.Application.models.notification;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;

namespace LantanaGroup.Link.DemoApiGateway.Services.Client
{
    public class NotificationService : INotificationService
    {
        private readonly HttpClient _httpClient;
        private readonly IOptions<GatewayConfig> _gatewayConfig;

        public NotificationService(HttpClient httpClient, IOptions<GatewayConfig> gatewayConfig)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _gatewayConfig = gatewayConfig ?? throw new ArgumentNullException(nameof(_gatewayConfig));           
        }

        public async Task<HttpResponseMessage> CreateNotification(NotificationMessage model)
        {
            _httpClient.BaseAddress = new Uri(_gatewayConfig.Value.NotificationServiceApiUrl);
            _httpClient.DefaultRequestHeaders.Accept.Clear();
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            // HTTP GET
            HttpResponseMessage response = await _httpClient.PostAsJsonAsync($"api/notification", model);

            return response;
        }

        public async Task<HttpResponseMessage> CreateNotificationConfiguration(NotificationConfigurationModel model)
        {
            _httpClient.BaseAddress = new Uri(_gatewayConfig.Value.NotificationServiceApiUrl);
            _httpClient.DefaultRequestHeaders.Accept.Clear();
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            //Call create notification configuration
            HttpResponseMessage response = await _httpClient.PostAsJsonAsync($"api/notification/configuration", model);
            return response;
        }

        public async Task<HttpResponseMessage> DeleteNotificationConfiguration(Guid id)
        {
            _httpClient.BaseAddress = new Uri(_gatewayConfig.Value.NotificationServiceApiUrl);
            _httpClient.DefaultRequestHeaders.Accept.Clear();
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            //Update config values
            HttpResponseMessage response = await _httpClient.DeleteAsync($"api/notification/configuration/{id}");
            return response;
        }

        public async Task<HttpResponseMessage> ListConfigurations(string? searchText, string? filterFacilityBy, string? sortBy, int pageSize = 10, int pageNumber = 1)
        {
            _httpClient.BaseAddress = new Uri(_gatewayConfig.Value.NotificationServiceApiUrl);
            _httpClient.DefaultRequestHeaders.Accept.Clear();
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            // HTTP GET
            HttpResponseMessage response = await _httpClient.GetAsync($"api/notification/configuration?searchText={searchText}&filterFacilityBy={filterFacilityBy}" +
                $"&sortBy={sortBy}&pageSize={pageSize}&pageNumber={pageNumber}");

            return response;
        }

        public async Task<HttpResponseMessage> ListNotifications(string? searchText, string? filterFacilityBy, string? filterNotificationTypeBy, DateTime? createdOnStart, DateTime? createdOnEnd, DateTime? sentOnStart, DateTime? sentOnEnd, string? sortBy, int pageSize = 10, int pageNumber = 1)
        {
            _httpClient.BaseAddress = new Uri(_gatewayConfig.Value.NotificationServiceApiUrl);
            _httpClient.DefaultRequestHeaders.Accept.Clear();
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            // HTTP GET
            HttpResponseMessage response = await _httpClient.GetAsync($"api/notification?searchText={searchText}&filterFacilityBy={filterFacilityBy}&filterNotificationTypeBy={filterNotificationTypeBy}" +
                $"&createdOnStart={createdOnStart}&createdOnEnd={createdOnEnd}&sentOnStart={sentOnStart}&sentOnEnd={sentOnEnd}" +
                $"&sortBy={sortBy}&pageSize={pageSize}&pageNumber={pageNumber}");

            return response;
        }

        public async Task<HttpResponseMessage> UpdateNotificationConfiguration(NotificationConfigurationModel model)
        {
            _httpClient.BaseAddress = new Uri(_gatewayConfig.Value.NotificationServiceApiUrl);
            _httpClient.DefaultRequestHeaders.Accept.Clear();
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            //Update config values
            HttpResponseMessage response = await _httpClient.PutAsJsonAsync($"api/notification/configuration", model);
            return response;
        }
    }
}
