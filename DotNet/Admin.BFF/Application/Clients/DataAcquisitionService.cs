using LantanaGroup.Link.LinkAdmin.BFF.Application.Commands.Security;
using LantanaGroup.Link.LinkAdmin.BFF.Application.Models.Configuration;
using LantanaGroup.Link.LinkAdmin.BFF.Application.Models.Health;
using LantanaGroup.Link.LinkAdmin.BFF.Infrastructure.Logging;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Net.Http.Json;
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
            var request = new HttpRequestMessage(HttpMethod.Patch, $"api/data/acquisition-logs/facility/{Uri.EscapeDataString(facilityId)}/restore");
                await SetAuthHeaderAsync(user, request, cancellationToken);
            return await _client.SendAsync(request, cancellationToken);
        }

        public async Task<HttpResponseMessage> RestoreLogsByReportTrackingIdAsync(ClaimsPrincipal user, string reportTrackingId, CancellationToken cancellationToken)
        {
            var request = new HttpRequestMessage(HttpMethod.Patch, $"api/data/acquisition-logs/report/{Uri.EscapeDataString(reportTrackingId)}/restore");
            await SetAuthHeaderAsync(user, request, cancellationToken);
            return await _client.SendAsync(request, cancellationToken);
        }

        public async Task<HttpResponseMessage> CancelLogsByFilterAsync(
            ClaimsPrincipal user,
            object filter,
            int minAgeHours,
            CancellationToken cancellationToken)
        {
            var request = new HttpRequestMessage(
                HttpMethod.Post,
                $"api/data/acquisition-logs/cancel-by-filter?minAgeHours={minAgeHours}")
            {
                Content = JsonContent.Create(filter)
            };
            await SetAuthHeaderAsync(user, request, cancellationToken);
            return await _client.SendAsync(request, cancellationToken);
        }

        public async Task<HttpResponseMessage> SoftDeleteLogsByReportTrackingIdAsync(ClaimsPrincipal user, string reportTrackingId, CancellationToken cancellationToken)
        {
            var request = new HttpRequestMessage(HttpMethod.Delete, $"api/data/acquisition-logs/report/{Uri.EscapeDataString(reportTrackingId)}");
            await SetAuthHeaderAsync(user, request, cancellationToken);
            return await _client.SendAsync(request, cancellationToken);
        }

        public async Task<HttpResponseMessage> SoftDeleteLogsAsync(ClaimsPrincipal user, string facilityId, CancellationToken cancellationToken)
        {
            var request = new HttpRequestMessage(HttpMethod.Delete, $"api/data/acquisition-logs/facility/{Uri.EscapeDataString(facilityId)}");
            await SetAuthHeaderAsync(user, request, cancellationToken);
            return await _client.SendAsync(request, cancellationToken);
        }

        private async Task SetAuthHeaderAsync(ClaimsPrincipal user, HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (_authenticationSchemaConfig.Value.EnableAnonymousAccess) return;
            using var scope = _scopeFactory.CreateScope();
            var createLinkBearerToken = scope.ServiceProvider.GetRequiredService<ICreateLinkBearerToken>();
            var token = await createLinkBearerToken.ExecuteAsync(user, 2, cancellationToken);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
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
