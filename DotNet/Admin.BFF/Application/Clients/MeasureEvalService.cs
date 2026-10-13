using LantanaGroup.Link.LinkAdmin.BFF.Infrastructure.Logging;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using LantanaGroup.Link.LinkAdmin.BFF.Application.Models.Health;

namespace LantanaGroup.Link.LinkAdmin.BFF.Application.Clients
{
    public class MeasureEvalService
    {
        private readonly ILogger<MeasureEvalService> _logger;
        private readonly HttpClient _client;
        private readonly IOptions<ServiceRegistry> _serviceRegistry;
        private readonly TimeSpan _healthCheckTimeout;

        public MeasureEvalService(ILogger<MeasureEvalService> logger, HttpClient client,
            IOptions<ServiceRegistry> serviceRegistry, IOptions<ServiceHealthCheckSettings> healthCheckSettings)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _serviceRegistry = serviceRegistry ?? throw new ArgumentNullException(nameof(serviceRegistry));
            _healthCheckTimeout = (healthCheckSettings ?? throw new ArgumentNullException(nameof(healthCheckSettings)))
                .Value.Timeout;

            InitHttpClient();
        }

        public async Task<HttpResponseMessage> ServiceHealthCheck(CancellationToken cancellationToken)
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(_healthCheckTimeout);

            // HTTP GET
            HttpResponseMessage response = await _client.GetAsync($"health", timeoutCts.Token);

            return response;
        }

        public Task<LinkServiceHealthReport> LinkServiceHealthCheck(CancellationToken cancellationToken) =>
            SpringServiceHealth.ReadAsync(_client, "Measure Evaluation", _healthCheckTimeout, _logger, cancellationToken);

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
