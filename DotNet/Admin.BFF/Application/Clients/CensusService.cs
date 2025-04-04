using LantanaGroup.Link.LinkAdmin.BFF.Infrastructure.Logging;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Security.Claims;
using LantanaGroup.Link.LinkAdmin.BFF.Application.Commands.Security;
using LantanaGroup.Link.LinkAdmin.BFF.Application.Models.Configuration;
using LantanaGroup.Link.LinkAdmin.BFF.Application.Models.Health;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace LantanaGroup.Link.LinkAdmin.BFF.Application.Clients
{
    public class CensusService
    {
        private readonly ILogger<CensusService> _logger;
        private readonly HttpClient _client;
        private readonly IOptions<ServiceRegistry> _serviceRegistry;
        private readonly IOptions<AuthenticationSchemaConfig> _authenticationSchemaConfig;
        private readonly IServiceScopeFactory _scopeFactory;

        public CensusService(ILogger<CensusService> logger, HttpClient client, IOptions<ServiceRegistry> serviceRegistry, IOptions<AuthenticationSchemaConfig> authenticationSchemaConfig, IServiceScopeFactory scopeFactory)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _serviceRegistry = serviceRegistry ?? throw new ArgumentNullException(nameof(serviceRegistry));
            _authenticationSchemaConfig = authenticationSchemaConfig ?? throw new ArgumentNullException(nameof(authenticationSchemaConfig));
            _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
           
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
                if (healthResult is not null) healthResult.Service = "Census";

                return healthResult;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Census service health check failed");
                return new LinkServiceHealthReport { Service = "Census", Status = HealthStatus.Unhealthy };
            }
        }

        private void InitHttpClient()
        {
            //check if the service uri is set
            if (string.IsNullOrEmpty(_serviceRegistry.Value.CensusServiceUrl))
            {
                _logger.LogGatewayServiceUriException("Census", "Census service uri is not set");
                throw new ArgumentNullException("Census Service URL is missing.");
            }

            _client.BaseAddress = new Uri(_serviceRegistry.Value.CensusServiceUrl);
            _client.DefaultRequestHeaders.Accept.Clear();
            _client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }
    }
}
