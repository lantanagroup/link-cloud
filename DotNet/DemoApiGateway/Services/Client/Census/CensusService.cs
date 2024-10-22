using LantanaGroup.Link.DemoApiGateway.Application.models;
using LantanaGroup.Link.DemoApiGateway.Application.models.census;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using LantanaGroup.Link.Census.Application.Models;

namespace LantanaGroup.Link.DemoApiGateway.Services.Client
{
    public class CensusService : ICensusService
    {
        //create a census service to handle the census api calls
        private readonly HttpClient _httpClient;
        private readonly IOptions<ServiceRegistry> _serviceRegistry;

        public CensusService(HttpClient httpClient, IOptions<ServiceRegistry> serviceRegistry)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _serviceRegistry = serviceRegistry ?? throw new ArgumentNullException(nameof(_serviceRegistry));
        }

        // * Create a new census
        // * @param {CensusConfigModel} model
        // * @returns {Promise<HttpResponseMessage>}
        public async Task<HttpResponseMessage> CreateCensus(CensusConfigModel model)
        {
            _httpClient.BaseAddress = new Uri(_serviceRegistry.Value.CensusServiceUrl);
            _httpClient.DefaultRequestHeaders.Accept.Clear();
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            HttpResponseMessage response = await _httpClient.PostAsJsonAsync($"api/census/config", model);

            return response;
        }

        // * Delete a census
        // * @param {string} censusId
        // * @returns {Promise<HttpResponseMessage>}
        public async Task<HttpResponseMessage> DeleteCensus(string censusId)
        {
            _httpClient.BaseAddress = new Uri(_serviceRegistry.Value.CensusServiceUrl);
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
            _httpClient.BaseAddress = new Uri(_serviceRegistry.Value.CensusServiceUrl);
            _httpClient.DefaultRequestHeaders.Accept.Clear();
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            HttpResponseMessage response = await _httpClient.GetAsync($"api/census/config/{censusId}");

            return response;
        }
        
        // * Update a census
        // * @param {string} censusId
        // * @param {CensusConfigModel} model
        // * @returns {Promise<HttpResponseMessage>}
        public async Task<HttpResponseMessage> UpdateCensus(string censusId, CensusConfigModel model)
        {
            _httpClient.BaseAddress = new Uri(_serviceRegistry.Value.CensusServiceUrl);
            _httpClient.DefaultRequestHeaders.Accept.Clear();
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            HttpResponseMessage response = await _httpClient.PutAsJsonAsync($"api/census/config/{censusId}", model);

            return response;
        } 
    }
}
