using LantanaGroup.Link.DemoApiGateway.Application.models;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;

namespace LantanaGroup.Link.DemoApiGateway.Services.Client
{
    public class ReportService : IReportService
    {
        //create a census service to handle the census api calls
        private readonly HttpClient _httpClient;
        private readonly IOptions<GatewayConfig> _gatewayConfig;

        public ReportService(HttpClient httpClient, IOptions<GatewayConfig> gatewayConfig)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _gatewayConfig = gatewayConfig ?? throw new ArgumentNullException(nameof(_gatewayConfig));
        }

        private void InitHttpClient()
        {
            _httpClient.BaseAddress = new Uri(_gatewayConfig.Value.ReportServiceApiUrl);
            _httpClient.DefaultRequestHeaders.Accept.Clear();
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }

        // * Create a new report
        // * @param {ReportConfigModel} model
        // * @returns {Promise<HttpResponseMessage>}
        public async Task<HttpResponseMessage> CreateReport(ReportConfigModel model)
        {
            InitHttpClient();

            HttpResponseMessage response = await _httpClient.PostAsJsonAsync($"api/ReportConfig/Create", model);

            return response;
        }

        // * Delete a report
        // * @param {string} reportId
        // * @returns {Promise<HttpResponseMessage>}
        public async Task<HttpResponseMessage> DeleteReport(string reportId)
        {
            InitHttpClient();

            HttpResponseMessage response = await _httpClient.DeleteAsync($"api/ReportConfig/Delete?id={reportId}");

            return response;
        }


        // * Get a report
        // * @param {string} reportId
        // * @returns {Promise<HttpResponseMessage>}
        public async Task<HttpResponseMessage> GetReport(string reportId)
        {

            InitHttpClient();

            HttpResponseMessage response = await _httpClient.GetAsync($"api/ReportConfig/Get?id={reportId}");

            return response;
        }

        // * Update a report
        // * @param {string} reportId
        // * @param {ReportConfigModel} model
        // * @returns {Promise<HttpResponseMessage>}
        public async Task<HttpResponseMessage> UpdateReport(string reportId, ReportConfigModel model)
        {
            InitHttpClient();

            HttpResponseMessage response = await _httpClient.PutAsJsonAsync($"api/ReportConfig/Update?id={reportId}", model);

            return response;
        }

        // * Get a report
        // * @param {string} reportId
        // * @returns {Promise<HttpResponseMessage>}
        public async Task<HttpResponseMessage> GetReports(string facilityId)
        {

            InitHttpClient();

            HttpResponseMessage response = await _httpClient.GetAsync($"api/ReportConfig/facility/{facilityId}");

            return response;
        }

    }
}
