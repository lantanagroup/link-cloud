using LantanaGroup.Link.Notification.Application.Interfaces.Clients;
using LantanaGroup.Link.Shared.Application.Interfaces.Services.Security.Token;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;

namespace LantanaGroup.Link.Notification.Presentation.Clients
{
    public class FacilityClient : IFacilityClient
    {
        private readonly HttpClient _httpClient;
        private readonly ICreateSystemToken _createSystemToken;
        private readonly IOptions<ServiceRegistry> _serviceRegistry;
        private readonly IOptions<LinkTokenServiceSettings> _linkTokenServiceConfig;

        public FacilityClient(HttpClient httpClient, ICreateSystemToken createSystemToken, IOptions<ServiceRegistry> serviceRegistry, IOptions<LinkTokenServiceSettings> linkTokenServiceConfig)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _createSystemToken = createSystemToken ?? throw new ArgumentNullException(nameof(createSystemToken));
            _serviceRegistry = serviceRegistry ?? throw new ArgumentNullException(nameof(serviceRegistry));
            _linkTokenServiceConfig = linkTokenServiceConfig ?? throw new ArgumentNullException(nameof(linkTokenServiceConfig));
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

            //TODO: add method to get key that includes looking at redis for future use case
            if(_linkTokenServiceConfig.Value.SigningKey is null)
                throw new Exception("Link Token Service Signing Key is missing.");

            //Add link token
            var token = _createSystemToken.ExecuteAsync(_linkTokenServiceConfig.Value.SigningKey, 2).Result;
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
    }
}
