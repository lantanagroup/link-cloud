using LantanaGroup.Link.LinkAdmin.BFF.Infrastructure.Logging;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using LantanaGroup.Link.LinkAdmin.BFF.Application.Models;
using LantanaGroup.Link.LinkAdmin.BFF.Application.Models.Health;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace LantanaGroup.Link.LinkAdmin.BFF.Application.Clients
{
    public class DataAcquisitionService
    {
        private readonly ILogger<DataAcquisitionService> _logger;
        private readonly HttpClient _client;
        private readonly IOptions<ServiceRegistry> _serviceRegistry;

        public DataAcquisitionService(ILogger<DataAcquisitionService> logger, HttpClient client, IOptions<ServiceRegistry> serviceRegistry)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _serviceRegistry = serviceRegistry ?? throw new ArgumentNullException(nameof(serviceRegistry));

            InitHttpClient();
        }

        public async Task<HttpResponseMessage> ServiceHealthCheck(CancellationToken cancellationToken)
        {           
            // HTTP GET
            HttpResponseMessage response = await _client.GetAsync($"health", cancellationToken);

            return response;
        }
        
        public async Task<LinkServiceHealthReport> LinkServiceHealthCheck(CancellationToken cancellationToken)
        {
            // HTTP GET
            try
            {
                var response = await _client.GetAsync($"health", cancellationToken);
                var healthResult = await response.Content.ReadFromJsonAsync<LinkServiceHealthReport>(cancellationToken: cancellationToken);
                if (healthResult is not null) healthResult.Service = "Data Acquisition";

                return healthResult;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Data Acquisition service health check failed");
                return new LinkServiceHealthReport { Service = "Data Acquisition", Status = HealthStatus.Unhealthy };
            }
        }

        private void InitHttpClient()
        {
            //check if the service uri is set
            if (string.IsNullOrEmpty(_serviceRegistry.Value.DataAcquisitionServiceUrl))
            {
                _logger.LogGatewayServiceUriException("DataAcquisition", "Data Acquisition service uri is not set");
                throw new ArgumentNullException("Data Acquisition Service URL is missing.");
            }

            _client.BaseAddress = new Uri(_serviceRegistry.Value.DataAcquisitionServiceUrl);
            _client.DefaultRequestHeaders.Accept.Clear();
            _client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }
    }
}
