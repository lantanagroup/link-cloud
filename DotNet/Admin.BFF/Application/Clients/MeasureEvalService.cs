using LantanaGroup.Link.LinkAdmin.BFF.Infrastructure.Logging;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using LantanaGroup.Link.LinkAdmin.BFF.Application.Models.Health;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace LantanaGroup.Link.LinkAdmin.BFF.Application.Clients
{
    public class MeasureEvalService
    {
        private readonly ILogger<MeasureEvalService> _logger;
        private readonly HttpClient _client;
        private readonly IOptions<ServiceRegistry> _serviceRegistry;

        public MeasureEvalService(ILogger<MeasureEvalService> logger, HttpClient client, IOptions<ServiceRegistry> serviceRegistry)
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

                //TODO: update when further functionality within the java services have been added
                if (response.IsSuccessStatusCode)
                {
                    return new LinkServiceHealthReport { Service = "Measure Evaluation", Status = HealthStatus.Healthy };
                }
                else
                {
                    return new LinkServiceHealthReport { Service = "Measure Evaluation", Status = HealthStatus.Unhealthy };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Measure Evaluation service health check failed");
                return new LinkServiceHealthReport { Service = "Measure Evaluation", Status = HealthStatus.Unhealthy };
            }
        }

        private void InitHttpClient()
        {
            //check if the service uri is set
            if (string.IsNullOrEmpty(_serviceRegistry.Value.MeasureServiceUrl))
            {
                _logger.LogGatewayServiceUriException("MeasureEvaluation", "Measure Evaluation service uri is not set");
                throw new ArgumentNullException("Measure Evaluation Service URL is missing.");
            }

            _client.BaseAddress = new Uri(_serviceRegistry.Value.MeasureServiceUrl);
            _client.DefaultRequestHeaders.Accept.Clear();
            _client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }
    }
}
