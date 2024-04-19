using LantanaGroup.Link.DemoApiGateway.Application.models;
using LantanaGroup.Link.DemoApiGateway.Application.models.dataAcquisition;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;

namespace LantanaGroup.Link.DemoApiGateway.Services.Client.DataAcquisition
{
    public class DataAcquisitionService : IDataAcquisitionService
    {
        private readonly HttpClient _httpClient;
        private readonly IOptions<ServiceRegistry> _serviceRegistry;

        public DataAcquisitionService(HttpClient httpClient, IOptions<ServiceRegistry> _serviceRegistry)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _serviceRegistry = _serviceRegistry ?? throw new ArgumentNullException(nameof(_serviceRegistry));           
        }

        private void InitHttpClient()
        {
            _httpClient.BaseAddress = new Uri(_serviceRegistry.Value.DataAcquisitionServiceUrl);
            _httpClient.DefaultRequestHeaders.Accept.Clear();
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }

        // * Create a new data acquisition configuration
        // * @param {DataAcquisitionConfigModel} model
        // * @param {QueryConfigurationTypePathParameter} queryType
        // * @returns {Promise<HttpResponseMessage>}
        public async Task<HttpResponseMessage> CreateDataAcquisitionAuthenticationConfiguration(string facilityId, QueryConfigurationTypePathParameter queryType, AuthenticationConfiguration model)
        {
            InitHttpClient();

            HttpResponseMessage response = await _httpClient.PostAsJsonAsync($"api/{facilityId}/{queryType}", model);

            return response;
        }

        // * Get a data acquisition configuration
        // * @param {string} facilityId
        // * @param {QueryConfigurationTypePathParameter} queryType
        // * @returns {Promise<HttpResponseMessage>}
        public async Task<HttpResponseMessage> GetDataAcquisitionAuthenticationConfiguration(string facilityId, QueryConfigurationTypePathParameter queryType)
        {
            InitHttpClient();

            HttpResponseMessage response = await _httpClient.GetAsync($"api/{facilityId}/{queryType}/authentication");

            return response;
        }        

        // * Update a data acquisition configuration
        // * @param {string} facilityId
        // * @param {QueryConfigurationTypePathParameter} queryType
        // * @param {AuthenticationConfiguration} model
        // * @returns {Promise<HttpResponseMessage>}
        public async Task<HttpResponseMessage> UpdateDataAcquisitionAuthenticationConfiguration(string facilityId, QueryConfigurationTypePathParameter queryType, AuthenticationConfiguration model)
        {
            InitHttpClient();

            HttpResponseMessage response = await _httpClient.PutAsJsonAsync($"api/{facilityId}/{queryType}/authentication", model);

            return response;
        }

        // * Delete a data acquisition configuration
        // * @param {string} facilityId
        // * @param {QueryConfigurationTypePathParameter} queryType
        // * @returns {Promise<HttpResponseMessage>}
        public async Task<HttpResponseMessage> DeleteDataAcquisitionAuthenticationConfiguration(string facilityId, QueryConfigurationTypePathParameter queryType)
        {
            InitHttpClient();

            HttpResponseMessage response = await _httpClient.DeleteAsync($"api/{facilityId}/{queryType}/authentication");

            return response;
        }

        // * Create a new data acquisition configuration
        // * @param {DataAcquisitionConfigModel} model
        // * @returns {Promise<HttpResponseMessage>}
        public async Task<HttpResponseMessage> CreateDataAcquisitionConfiguration(DataAcquisitionConfigModel model)
        {
            InitHttpClient();

            HttpResponseMessage response = await _httpClient.PostAsJsonAsync($"api/data/config", model);

            return response;
        }

        // * Get a data acquisition configuration
        // * @param {string} facilityId
        // * @returns {Promise<HttpResponseMessage>}
        public async Task<HttpResponseMessage> GetDataAcquisitionConfiguration(string facilityId)
        {
            InitHttpClient();

            HttpResponseMessage response = await _httpClient.GetAsync($"api/data/config/{facilityId}");

            return response;
        }

        // * Update a data acquisition configuration
        // * @param {string} facilityId
        // * @param {DataAcquisitionConfigModel} model
        // * @returns {Promise<HttpResponseMessage>}
        public async Task<HttpResponseMessage> UpdateDataAcquisitionConfiguration(string facilityId, DataAcquisitionConfigModel model)
        {
            InitHttpClient();

            HttpResponseMessage response = await _httpClient.PutAsJsonAsync($"api/data/config/{facilityId}", model);

            return response;
        }

        // * Delete a data acquisition configuration
        // * @param {string} facilityId
        // * @returns {Promise<HttpResponseMessage>}
        public async Task<HttpResponseMessage> DeleteDataAcquisitionConfiguration(string facilityId)
        {
            InitHttpClient();

            HttpResponseMessage response = await _httpClient.DeleteAsync($"api/data/config/{facilityId}");

            return response;
        }

        // * Create a new data acquisition query configuration
        // * @param {DataAcquisitionQueryConfigModel} model
        // * @returns {Promise<HttpResponseMessage>}
        public async Task<HttpResponseMessage> CreateDataAcquisitionQueryConfig(DataAcquisitionQueryConfigModel model)
        {
            InitHttpClient();

            HttpResponseMessage response = await _httpClient.PostAsJsonAsync($"api/fhirQueryConfiguration", model);

            return response;
            
        }

        // * Get a data acquisition query configuration
        // * @param {string} facilityId
        // * @returns {Promise<HttpResponseMessage>}
        public async Task<HttpResponseMessage> GetDataAcquisitionQueryConfig(string facilityId)
        {
            InitHttpClient();

            HttpResponseMessage response = await _httpClient.GetAsync($"api/{facilityId}/fhirQueryConfiguration");

            return response;
        }

        // * Update a data acquisition query configuration
        // * @param {string} facilityId
        // * @param {DataAcquisitionQueryConfigModel} model
        // * @returns {Promise<HttpResponseMessage>}
        public async Task<HttpResponseMessage> UpdateDataAcquisitionQueryConfig(string facilityId, DataAcquisitionQueryConfigModel model)
        {
            InitHttpClient();

            HttpResponseMessage response = await _httpClient.PutAsJsonAsync($"api/fhirQueryConfiguration", model);

            return response;
        }

        // * Delete a data acquisition query configuration
        // * @param {string} facilityId
        // * @returns {Promise<HttpResponseMessage>}
        public async Task<HttpResponseMessage> DeleteDataAcquisitionQueryConfig(string facilityId)
        {
            InitHttpClient();

            HttpResponseMessage response = await _httpClient.DeleteAsync($"api/{facilityId}/fhirQueryConfiguration");

            return response;
        }

        // * Create a new data acquisition query plan
        // * @param {string} facilityId
        // * @param {string} queryPlanType
        // * @param {DataAcquisitionQueryPlanModel} model
        // * @returns {Promise<HttpResponseMessage>}
        public async Task<HttpResponseMessage> CreateDataAcquisitionQueryPlan(string facilityId, string queryPlanType, DataAcquisitionQueryPlanModel model)
        {
            InitHttpClient();

            HttpResponseMessage response = await _httpClient.PostAsJsonAsync($"api/{facilityId}/{queryPlanType}", model);   

            return response;
        }

        // * Get a data acquisition query plan
        // * @param {string} facilityId
        // * @param {string} queryPlanType
        // * @returns {Promise<HttpResponseMessage>}
        public async Task<HttpResponseMessage> GetDataAcquisitionQueryPlan(string facilityId, string queryPlanType)
        {
            InitHttpClient();

            HttpResponseMessage response = await _httpClient.GetAsync($"api/{facilityId}/{queryPlanType}");

            return response;
        }

        // * Update a data acquisition query plan
        // * @param {string} facilityId
        // * @param {string} queryPlanType
        // * @param {DataAcquisitionQueryPlanModel} model
        // * @returns {Promise<HttpResponseMessage>}
        public async Task<HttpResponseMessage> UpdateDataAcquisitionQueryPlan(string facilityId, string queryPlanType, DataAcquisitionQueryPlanModel model)
        {
            InitHttpClient();

            HttpResponseMessage response = await _httpClient.PutAsJsonAsync($"api/{facilityId}/{queryPlanType}", model);

            return response;
        }

        // * Delete a data acquisition query plan
        // * @param {string} facilityId
        // * @param {string} queryPlanType
        // * @returns {Promise<HttpResponseMessage>}
        public async Task<HttpResponseMessage> DeleteDataAcquisitionQueryPlan(string facilityId, string queryPlanType)
        {
            InitHttpClient();

            HttpResponseMessage response = await _httpClient.DeleteAsync($"api/{facilityId}/{queryPlanType}");

            return response;
        }
        
        // * Get a data acquisition fhir list
        // * @param {string} facilityId
        // * @returns {Promise<HttpResponseMessage>}
        public async Task<HttpResponseMessage> GetDataAcquisitionFhirList(string facilityId)
        {
            InitHttpClient();

            HttpResponseMessage response = await _httpClient.GetAsync($"api/{facilityId}/fhirQueryList");

            return response;
        }

        public async Task<HttpResponseMessage> PostDataAcquisitionFhirList(DataAcquisitionQueryListConfigModel model)
        {
            InitHttpClient();

            HttpResponseMessage response = await _httpClient.PostAsJsonAsync($"api/{model.FacilityId}/fhirQueryList", model);

            return response;
        }
    }
}
