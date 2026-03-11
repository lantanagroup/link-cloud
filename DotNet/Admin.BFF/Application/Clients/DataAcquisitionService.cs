using LantanaGroup.Link.LinkAdmin.BFF.Application.Commands.Security;
using LantanaGroup.Link.LinkAdmin.BFF.Application.Models.Configuration;
using LantanaGroup.Link.LinkAdmin.BFF.Application.Models.Health;
using LantanaGroup.Link.LinkAdmin.BFF.Infrastructure.Logging;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Security.Claims;

namespace LantanaGroup.Link.LinkAdmin.BFF.Application.Clients
{
    public class DataAcquisitionService
    {
        private readonly ILogger<DataAcquisitionService> _logger;
        private readonly HttpClient _client;
        private readonly IOptions<ServiceRegistry> _serviceRegistry;
        private readonly IOptions<AuthenticationSchemaConfig> _authenticationSchemaConfig;
        private readonly IServiceScopeFactory _scopeFactory;

        public DataAcquisitionService(ILogger<DataAcquisitionService> logger, HttpClient client, IOptions<ServiceRegistry> serviceRegistry, IOptions<AuthenticationSchemaConfig> authenticationSchemaConfig, IServiceScopeFactory scopeFactory)
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
                if (healthResult is not null) healthResult.Service = "Data Acquisition";

                return healthResult;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Data Acquisition service health check failed");
                return new LinkServiceHealthReport { Service = "Data Acquisition", Status = HealthStatus.Unhealthy };
            }
        }

        public async Task<HttpResponseMessage> RestoreLogsAsync(ClaimsPrincipal user, string facilityId, CancellationToken cancellationToken)
        {
            if (!_authenticationSchemaConfig.Value.EnableAnonymousAccess)
            {
                var createLinkBearerToken = _scopeFactory.CreateScope().ServiceProvider.GetRequiredService<ICreateLinkBearerToken>();
                var token = await createLinkBearerToken.ExecuteAsync(user, 2);
                _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }

            return await _client.PatchAsync($"api/data/acquisition-logs/facility/{Uri.EscapeDataString(facilityId)}/restore", null, cancellationToken);
        }

        public async Task<HttpResponseMessage> SoftDeleteLogsAsync(ClaimsPrincipal user, string facilityId, CancellationToken cancellationToken)
        {
            if (!_authenticationSchemaConfig.Value.EnableAnonymousAccess)
            {
                var createLinkBearerToken = _scopeFactory.CreateScope().ServiceProvider.GetRequiredService<ICreateLinkBearerToken>();
                var token = await createLinkBearerToken.ExecuteAsync(user, 2);
                _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }

            return await _client.DeleteAsync($"api/data/acquisition-logs/facility/{Uri.EscapeDataString(facilityId)}", cancellationToken);
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
