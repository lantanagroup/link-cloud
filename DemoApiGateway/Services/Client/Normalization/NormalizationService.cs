using LantanaGroup.Link.DemoApiGateway.Application.models;
using LantanaGroup.Link.DemoApiGateway.Application.models.normalization;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;

namespace LantanaGroup.Link.DemoApiGateway.Services.Client.Normalization
{
    public class NormalizationService : INormalizationService
    {
        private readonly HttpClient _httpClient;
        private readonly IOptions<GatewayConfig> _gatewayConfig;

        public NormalizationService(HttpClient httpClient, IOptions<GatewayConfig> gatewayConfig)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _gatewayConfig = gatewayConfig ?? throw new ArgumentNullException(nameof(_gatewayConfig));           
        }

        private void InitHttpClient()
        {
            _httpClient.BaseAddress = new Uri(_gatewayConfig.Value.NormalizationServiceApiUrl);
            _httpClient.DefaultRequestHeaders.Accept.Clear();
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }

        // * Create a new normalization configuration
        // * @param {DataAcquisitionConfigModel} model
        // * @returns {Promise<HttpResponseMessage>}
        public async Task<HttpResponseMessage> CreateNormalizationConfig(NormalizationConfigModel model)
        {
            InitHttpClient();
           
            HttpResponseMessage response = await _httpClient.PostAsJsonAsync($"api/normalization", model);

            return response;
        }

        // * Get a normalization configuration
        // * @param {string} facilityId
        // * @returns {Promise<HttpResponseMessage>}
        public async Task<HttpResponseMessage> GetNormalizationConfig(string facilityId)
        {
            InitHttpClient();
            
            HttpResponseMessage response = await _httpClient.GetAsync($"api/normalization/{facilityId}");

            return response;
        }

        // * Update a normalization configuration
        // * @param {string} facilityId
        // * @param {NormalizationConfigModel} model
        // * @returns {Promise<HttpResponseMessage>}
        public async Task<HttpResponseMessage> UpdateNormalizationConfig(string facilityId, NormalizationConfigModel model)
        {
            InitHttpClient();
            
            HttpResponseMessage response = await _httpClient.PutAsJsonAsync($"api/normalization/{facilityId}", model);

            return response;
        }

        // * Delete a normalization configuration
        // * @param {string} facilityId
        // * @returns {Promise<HttpResponseMessage>}
        public async Task<HttpResponseMessage> DeleteNormalizationConfig(string facilityId)
        {
            InitHttpClient();
            
            HttpResponseMessage response = await _httpClient.DeleteAsync($"api/normalization/{facilityId}");

            return response;
        }
    }
}
