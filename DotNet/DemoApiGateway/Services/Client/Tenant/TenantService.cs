using LantanaGroup.Link.DemoApiGateway.Application.models;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;

namespace LantanaGroup.Link.DemoApiGateway.Services.Client
{
    public class TenantService : ITenantService
    {
        private readonly HttpClient _httpClient;
        private readonly IOptions<ServiceRegistry> _serviceRegistry;

        public TenantService(HttpClient httpClient, IOptions<ServiceRegistry> serviceRegistry)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _serviceRegistry = serviceRegistry ?? throw new ArgumentNullException(nameof(_serviceRegistry));
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

        // * Create a new facility
        // * @param {FacilityConfigModel} model
        // * @returns {Promise<HttpResponseMessage>}       
        public async Task<HttpResponseMessage> CreateFacility(FacilityConfigModel model)
        {
            InitHttpClient();

            HttpResponseMessage response = await _httpClient.PostAsJsonAsync($"api/Facility", model);

            return response;
        }

        // * Delete a facility
        // * @param {string} facilityId
        // * @returns {Promise<HttpResponseMessage>}
        public Task<HttpResponseMessage> DeleteFacility(string facilityId)
        {
            InitHttpClient();

            HttpResponseMessage response = _httpClient.DeleteAsync($"api/Facility/{facilityId}").Result;
            return Task.FromResult(response);            
        }

        // * Get a facility
        // * @param {string} facilityId
        // * @returns {Promise<HttpResponseMessage>}
        public Task<HttpResponseMessage> GetFacility(string facilityId)
        {
            InitHttpClient();

            HttpResponseMessage response = _httpClient.GetAsync($"api/Facility/{facilityId}").Result;
            return Task.FromResult(response);
        }

        // * List facilities
        // * @param {string} facilityId
        // * @param {string} facilityName
        // * @param {string} sortBy
        // * @param {number} pageSize
        // * @param {number} pageNumber
        // * @returns {Promise<HttpResponseMessage>}
        public async Task<HttpResponseMessage> ListFacilities(string? facilityId, string? facilityName, string? sortBy, int pageSize = 10, int pageNumber = 1)
        {
            InitHttpClient();

            HttpResponseMessage response = await _httpClient.GetAsync($"api/Facility?facilityId={facilityId}&facilityName={facilityName}&sortBy={sortBy}&pageSize={pageSize}&pageNumber={pageNumber}");
            return response;
        }

        // * Update a facility
        // * @param {string} facilityId
        // * @param {FacilityConfigModel} model
        // * @returns {Promise<HttpResponseMessage>}
        public Task<HttpResponseMessage> UpdateFacility(string facilityId, FacilityConfigModel model)
        {
            InitHttpClient();

            HttpResponseMessage response = _httpClient.PutAsJsonAsync($"api/Facility/{facilityId}", model).Result;
            return Task.FromResult(response);
        }
    }
}
