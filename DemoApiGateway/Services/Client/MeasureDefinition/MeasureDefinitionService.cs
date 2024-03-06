using LantanaGroup.Link.DemoApiGateway.Application.models;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;

namespace LantanaGroup.Link.DemoApiGateway.Services.Client
{
    public class MeasureDefinitionService : IMeasureDefinitionService
    {
        //create a census service to handle the census api calls
        private readonly HttpClient _httpClient;
        private readonly IOptions<GatewayConfig> _gatewayConfig;

        public MeasureDefinitionService(HttpClient httpClient, IOptions<GatewayConfig> gatewayConfig)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _gatewayConfig = gatewayConfig ?? throw new ArgumentNullException(nameof(_gatewayConfig));
        }

        private void InitHttpClient()
        {
            _httpClient.BaseAddress = new Uri(_gatewayConfig.Value.MeasureServiceApiUrl);
            _httpClient.DefaultRequestHeaders.Accept.Clear();
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }

        // * Create a new measure definition
        // * @param {ReportConfigModel} model
        // * @returns {Promise<HttpResponseMessage>}
        public async Task<HttpResponseMessage> CreateMeasureDefinition(MeasureDefinitionConfigModel model)
        {
            InitHttpClient();

            HttpResponseMessage response = await _httpClient.PostAsJsonAsync($"api/measureDef", model);

            return response;
        }

        // * Delete a measure definition
        // * @param {string} measureDefinitionId
        // * @returns {Promise<HttpResponseMessage>}
        public async Task<HttpResponseMessage> DeleteMeasureDefinition(string measureDefinitionId)
        {
            InitHttpClient();

            HttpResponseMessage response = await _httpClient.DeleteAsync($"api/measureDef/{measureDefinitionId}");

            return response;
        }


        // * Get a measure definition
        // * @param {string} measureDefinitionId
        // * @returns {Promise<HttpResponseMessage>}
        public async Task<HttpResponseMessage> GetMeasureDefinition(string measureDefinitionId)
        {

            InitHttpClient();

            HttpResponseMessage response = await _httpClient.GetAsync($"api/measureDef/{measureDefinitionId}");

            return response;
        }

        // * Get a measure definition
        // * @param {string} measureDefinitionId
        // * @returns {Promise<HttpResponseMessage>}
        public async Task<HttpResponseMessage> GetMeasureDefinitions(string measureDefinitionId)
        {

            InitHttpClient();

            HttpResponseMessage response = await _httpClient.GetAsync($"api/measureDef/measureDefinitions");

            return response;
        }

        // * Update a measure definition
        // * @param {string} measureDefinitionId
        // * @param {ReportConfigModel} model
        // * @returns {Promise<HttpResponseMessage>}
        public async Task<HttpResponseMessage> UpdateMeasureDefinition(string measureDefinitionId, MeasureDefinitionConfigModel model)
        {
            InitHttpClient();

            HttpResponseMessage response = await _httpClient.PutAsJsonAsync($"api/measureDef/{measureDefinitionId}", model);

            return response;
        }

        // * Get Measure Definitions
        // * @param {string} reportId
        // * @returns {Promise<HttpResponseMessage>}
        public async Task<HttpResponseMessage> GetMeasureDefinitions()
        {

            InitHttpClient();

            HttpResponseMessage response = await _httpClient.GetAsync($"api/measureDef/measureDefinitions");

            return response;
        }

    }
}
