using LantanaGroup.Link.LinkAdmin.BFF.Application.Commands.Security;
using LantanaGroup.Link.LinkAdmin.BFF.Application.Models.Configuration;
using LantanaGroup.Link.LinkAdmin.BFF.Application.Models.Health;
using LantanaGroup.Link.LinkAdmin.BFF.Infrastructure.Logging;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.Models.Tenant;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;

namespace LantanaGroup.Link.LinkAdmin.BFF.Application.Clients
{
    public class TenantService
    {
        private readonly ILogger<TenantService> _logger;
        private readonly HttpClient _client;
        private readonly IOptions<ServiceRegistry> _serviceRegistry;
        private readonly IOptions<AuthenticationSchemaConfig> _authenticationSchemaConfig;
        private readonly IServiceScopeFactory _scopeFactory;

        public TenantService(ILogger<TenantService> logger, HttpClient client, IOptions<ServiceRegistry> serviceRegistry, IOptions<AuthenticationSchemaConfig> authenticationSchemaConfig, IServiceScopeFactory scopeFactory)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _serviceRegistry = serviceRegistry ?? throw new ArgumentNullException(nameof(serviceRegistry));

            _authenticationSchemaConfig = authenticationSchemaConfig;
            _scopeFactory = scopeFactory;

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
                if (healthResult is not null) healthResult.Service = "Tenant";

                return healthResult;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Tenant service health check failed");
                return new LinkServiceHealthReport { Service = "Tenant", Status = HealthStatus.Unhealthy };
            }
        }
        
        private void InitHttpClient()
        {
            //check if the service uri is set
            if (string.IsNullOrEmpty(_serviceRegistry.Value.TenantService.TenantServiceUrl))
            {
                _logger.LogGatewayServiceUriException("Tenant", "Tenant service uri is not set");
                throw new ArgumentNullException("Tenant Service URL is missing.");
            }

            _client.BaseAddress = new Uri(_serviceRegistry.Value.TenantService.TenantServiceUrl);
            _client.DefaultRequestHeaders.Accept.Clear();
            _client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }
    }
}
