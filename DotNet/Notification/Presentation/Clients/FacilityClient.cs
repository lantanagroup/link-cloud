using LantanaGroup.Link.Notification.Application.Interfaces.Clients;
using System.Net.Http.Headers;
using System.Net.Http;
using Microsoft.Extensions.Options;
using LantanaGroup.Link.Notification.Application.Models;

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
            _httpClient.BaseAddress = new Uri(_serviceRegistry.Value.TenantServiceApiUrl);
            _httpClient.DefaultRequestHeaders.Accept.Clear();
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }
    }
}
