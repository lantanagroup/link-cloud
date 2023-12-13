using LantanaGroup.Link.DemoApiGateway.Application.models;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;

namespace LantanaGroup.Link.DemoApiGateway.Services.Client
{
    public class AuditService : IAuditService
    {
        private readonly HttpClient _httpClient;
        private readonly IOptions<GatewayConfig> _gatewayConfig;

        public AuditService(HttpClient httpClient, IOptions<GatewayConfig> gatewayConfig)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _gatewayConfig = gatewayConfig ?? throw new ArgumentNullException(nameof(_gatewayConfig));          
        }
        public async Task<HttpResponseMessage> ListAuditEvents(string? searchText, string? filterFacilityBy, string? filterCorrelationBy, string? filterServiceBy, string? filterActionBy, Guid? filterUserBy, string? sortBy, int pageSize = 10, int pageNumber = 1)
        {

            _httpClient.BaseAddress = new Uri(_gatewayConfig.Value.AuditServiceApiUrl);
            _httpClient.DefaultRequestHeaders.Accept.Clear();
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            // HTTP GET
            HttpResponseMessage response = await _httpClient.GetAsync($"api/audit?searchText={searchText}&filterFacilityBy={filterFacilityBy}" +
                $"&filterCorrelationBy={filterCorrelationBy}&filterServiceBy={filterServiceBy}&filterActionBy={filterActionBy}&filterUserBy={filterUserBy}" +
                $"&sortBy={sortBy}&pageSize={pageSize}&pageNumber={pageNumber}");

            return response;
        }
    }
}
