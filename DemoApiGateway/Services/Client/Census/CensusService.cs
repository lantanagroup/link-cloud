using LantanaGroup.Link.DemoApiGateway.Application.models;
using LantanaGroup.Link.DemoApiGateway.Application.models.census;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;

namespace LantanaGroup.Link.DemoApiGateway.Services.Client
{
    public class CensusService : ICensusService
    {
        //create a census service to handle the census api calls
        private readonly HttpClient _httpClient;
        private readonly IOptions<GatewayConfig> _gatewayConfig;

        public CensusService(HttpClient httpClient, IOptions<GatewayConfig> gatewayConfig)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _gatewayConfig = gatewayConfig ?? throw new ArgumentNullException(nameof(_gatewayConfig));
        }

        // * Create a new census
        // * @param {CensusConfigModel} model
        // * @returns {Promise<HttpResponseMessage>}
        public async Task<HttpResponseMessage> CreateCensus(CensusConfigModel model)
        {
            _httpClient.BaseAddress = new Uri(_gatewayConfig.Value.CensusServiceApiUrl);
            _httpClient.DefaultRequestHeaders.Accept.Clear();
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            HttpResponseMessage response = await _httpClient.PostAsJsonAsync($"api/census", model);

            return response;
        }

        // * Delete a census
        // * @param {string} censusId
        // * @returns {Promise<HttpResponseMessage>}
        public async Task<HttpResponseMessage> DeleteCensus(string censusId)
        {
            _httpClient.BaseAddress = new Uri(_gatewayConfig.Value.CensusServiceApiUrl);
            _httpClient.DefaultRequestHeaders.Accept.Clear();
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            HttpResponseMessage response = await _httpClient.DeleteAsync($"api/census/{censusId}");

            return response;
        }

        // * Get a census
        // * @param {string} censusId
        // * @returns {Promise<HttpResponseMessage>}
        public async Task<HttpResponseMessage> GetCensus(string censusId)
        {
            _httpClient.BaseAddress = new Uri(_gatewayConfig.Value.CensusServiceApiUrl);
            _httpClient.DefaultRequestHeaders.Accept.Clear();
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            HttpResponseMessage response = await _httpClient.GetAsync($"api/census/{censusId}");

            return response;
        }
        
        // * Update a census
        // * @param {string} censusId
        // * @param {CensusConfigModel} model
        // * @returns {Promise<HttpResponseMessage>}
        public async Task<HttpResponseMessage> UpdateCensus(string censusId, CensusConfigModel model)
        {
            _httpClient.BaseAddress = new Uri(_gatewayConfig.Value.CensusServiceApiUrl);
            _httpClient.DefaultRequestHeaders.Accept.Clear();
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            HttpResponseMessage response = await _httpClient.PutAsJsonAsync($"api/census/{censusId}", model);

            return response;
        } 
    }
}
