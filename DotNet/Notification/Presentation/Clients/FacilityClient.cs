using LantanaGroup.Link.Notification.Application.Interfaces.Clients;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;

namespace LantanaGroup.Link.Notification.Presentation.Clients
{
    public class FacilityClient : IFacilityClient
    {
        private readonly HttpClient _httpClient;
        private readonly IOptions<ServiceRegistry> _serviceRegistry;

        public FacilityClient(HttpClient httpClient, IOptions<ServiceRegistry> serviceRegistry)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _serviceRegistry = serviceRegistry ?? throw new ArgumentNullException(nameof(serviceRegistry));
        }

        public async Task<HttpResponseMessage> VerifyFacilityExists(string facilityId)
        {
            InitHttpClient();

            HttpResponseMessage response = await _httpClient.GetAsync($"api/Facility/{facilityId}");

            return response;
        }

        private void InitHttpClient()
        {
            if (_serviceRegistry.Value.TenantService is null)
                throw new Exception("Tenant Service configuration is missing.");
            else if (_serviceRegistry.Value.TenantService.TenantServiceUrl is null)
                throw new Exception("Tenant Service URL is missing.");

            _httpClient.BaseAddress = new Uri(_serviceRegistry.Value.TenantService.TenantServiceUrl);
            _httpClient.DefaultRequestHeaders.Accept.Clear();
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }
    }
}
