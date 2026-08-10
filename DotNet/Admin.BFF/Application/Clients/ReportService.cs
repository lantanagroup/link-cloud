using Hl7.Fhir.Model;
using LantanaGroup.Link.LinkAdmin.BFF.Application.Commands.Security;
using LantanaGroup.Link.LinkAdmin.BFF.Application.Models.Configuration;
using LantanaGroup.Link.LinkAdmin.BFF.Application.Models.Health;
using LantanaGroup.Link.LinkAdmin.BFF.Infrastructure.Logging;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.Services.Security;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Threading;

namespace LantanaGroup.Link.LinkAdmin.BFF.Application.Clients
{
    public class ReportService
    {
        private readonly ILogger<ReportService> _logger;
        private readonly HttpClient _client;
        private readonly IOptions<ServiceRegistry> _serviceRegistry;
        private readonly IOptions<AuthenticationSchemaConfig> _authenticationSchemaConfig;
        private readonly IServiceScopeFactory _scopeFactory;

        public ReportService(ILogger<ReportService> logger, HttpClient client, IOptions<ServiceRegistry> serviceRegistry, IOptions<AuthenticationSchemaConfig> authenticationSchemaConfig, IServiceScopeFactory scopeFactory)
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
            var response = await _client.GetAsync($"health", cancellationToken);

            return response;
        }

        public async Task<LinkServiceHealthReport> LinkServiceHealthCheck(CancellationToken cancellationToken)
        {
            // HTTP GET
            try
            {
                var response = await _client.GetAsync($"health", cancellationToken);
                var healthResult = await response.Content.ReadFromJsonAsync<LinkServiceHealthReport>(cancellationToken: cancellationToken);
                if (healthResult is not null) healthResult.Service = "Report";

                return healthResult;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Report service health check failed");
                return new LinkServiceHealthReport { Service = "Report", Status = HealthStatus.Unhealthy };
            }
        }

        public async Task<HttpResponseMessage> ReportSummaryList(
            ClaimsPrincipal user,
            CancellationToken cancellationToken,
            string? facilityId = null,
            Frequency? frequency = null,
            string? reportType = null,
            DateTime? reportStartDate = null,
            DateTime? reportEndDate = null,
            ScheduleStatus[]? statuses = null,
            bool? endOfReportPeriodJobHasRun = null,
            bool includeDeleted = false,
            string? sortBy = null,
            SortOrder? sortOrder = null,
            int pageNumber = 1,
            int pageSize = 10,
            DateOnly? createDate = null,
            string? reportScheduleId = null
            )
        {
            // HTTP GET
            if (!_authenticationSchemaConfig.Value.EnableAnonymousAccess)
            {
                var createLinkBearerToken = _scopeFactory.CreateScope().ServiceProvider.GetRequiredService<ICreateLinkBearerToken>();

                //create a bearer token for the system account
                var token = await createLinkBearerToken.ExecuteAsync(user, 2, cancellationToken);
                _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }

            pageNumber = Math.Max(1, pageNumber);
            pageSize = Math.Clamp(pageSize, 1, 100);

            // Build query parameters dynamically
            var queryParams = new Dictionary<string, string>
            {
                ["pageNumber"] = pageNumber.ToString(),
                ["pageSize"] = pageSize.ToString()
            };

            if (!string.IsNullOrWhiteSpace(facilityId))
            {
                queryParams["facilityId"] = facilityId;
            }

            if (frequency.HasValue)
            {
                queryParams["frequency"] = frequency.Value.ToString();
            }

            if (!string.IsNullOrWhiteSpace(reportType))
            {
                queryParams["reportType"] = reportType;
            }

            if (reportStartDate.HasValue)
            {
                queryParams["reportStartDate"] = reportStartDate.Value.ToString("o");
            }

            if (reportEndDate.HasValue)
            {
                queryParams["reportEndDate"] = reportEndDate.Value.ToString("o");
            }

            // status handled separately below (supports multiple values)

            if (endOfReportPeriodJobHasRun.HasValue)
            {
                queryParams["endOfReportPeriodJobHasRun"] = endOfReportPeriodJobHasRun.Value.ToString();
            }

            if (includeDeleted)
            {
                queryParams["includeDeleted"] = includeDeleted.ToString();
            }

            if (!string.IsNullOrWhiteSpace(sortBy))
            {
                queryParams["sortBy"] = sortBy;
            }

            if (sortOrder.HasValue)
            {
                queryParams["sortOrder"] = sortOrder.Value.ToString();
            }

            if (createDate.HasValue)
            {
                queryParams["createDate"] = createDate.Value.ToString("yyyy-MM-dd");
            }

            if (!string.IsNullOrWhiteSpace(reportScheduleId))
            {
                queryParams["id"] = reportScheduleId;
            }

            var relativeUrl = QueryHelpers.AddQueryString("api/schedules/search", queryParams);
            if (statuses != null && statuses.Length > 0)
            {
                foreach (var s in statuses)
                    relativeUrl = QueryHelpers.AddQueryString(relativeUrl, "status", s.ToString());
            }

            var response = await _client.GetAsync(relativeUrl, cancellationToken);

            return response;
        }

        public async Task<HttpResponseMessage> GetPatientInCensusCount(
            ClaimsPrincipal user,
            CancellationToken cancellationToken,
            string reportScheduleId)
        {
            // HTTP GET
            if (!_authenticationSchemaConfig.Value.EnableAnonymousAccess)
            {
                var createLinkBearerToken = _scopeFactory.CreateScope().ServiceProvider.GetRequiredService<ICreateLinkBearerToken>();

                //create a bearer token for the system account
                var token = await createLinkBearerToken.ExecuteAsync(user, 2, cancellationToken);
                _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }

            var relativeUrl = $"api/entries/schedules/{reportScheduleId}/count";

            var response = await _client.GetAsync(relativeUrl, cancellationToken);

            return response;
        }

        public async Task<HttpResponseMessage> GetReportPopulationsByReportScheduleId(
            ClaimsPrincipal user,
            CancellationToken cancellationToken,
            string reportScheduleId)
        {
            // HTTP GET
            if (!_authenticationSchemaConfig.Value.EnableAnonymousAccess)
            {
                var createLinkBearerToken = _scopeFactory.CreateScope().ServiceProvider.GetRequiredService<ICreateLinkBearerToken>();

                //create a bearer token for the system account
                var token = await createLinkBearerToken.ExecuteAsync(user, 2, cancellationToken);
                _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }

            var relativeUrl = $"api/populations/schedules/{reportScheduleId}/initial-population-count";

            var response = await _client.GetAsync(relativeUrl, cancellationToken);

            return response;
        }

        public async Task<HttpResponseMessage> GetReportScheduleById(
            ClaimsPrincipal user,
            CancellationToken cancellationToken,
            string reportScheduleId)
        {
            // HTTP GET
            if (!_authenticationSchemaConfig.Value.EnableAnonymousAccess)
            {
                var createLinkBearerToken = _scopeFactory.CreateScope().ServiceProvider.GetRequiredService<ICreateLinkBearerToken>();

                //create a bearer token for the system account
                var token = await createLinkBearerToken.ExecuteAsync(user, 2, cancellationToken);
                _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }

            var relativeUrl = $"api/schedules/{Uri.EscapeDataString(reportScheduleId ?? string.Empty)}";

            var response = await _client.GetAsync(relativeUrl, cancellationToken);

            return response;
        }

        public async Task<HttpResponseMessage> RestoreReportSchedulesAsync(ClaimsPrincipal user, string facilityId, CancellationToken cancellationToken)
        {
            if (!_authenticationSchemaConfig.Value.EnableAnonymousAccess)
            {
                var createLinkBearerToken = _scopeFactory.CreateScope().ServiceProvider.GetRequiredService<ICreateLinkBearerToken>();
                var token = await createLinkBearerToken.ExecuteAsync(user, 2, cancellationToken);
                _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }

            return await _client.PatchAsync($"api/schedules/facility/{Uri.EscapeDataString(facilityId)}/status?deleted=false", null, cancellationToken);
        }

        public async Task<HttpResponseMessage> GetActiveReportSchedulesAsync(ClaimsPrincipal user, string facilityId, CancellationToken cancellationToken)
        {
            if (!_authenticationSchemaConfig.Value.EnableAnonymousAccess)
            {
                var createLinkBearerToken = _scopeFactory.CreateScope().ServiceProvider.GetRequiredService<ICreateLinkBearerToken>();
                var token = await createLinkBearerToken.ExecuteAsync(user, 2, cancellationToken);
                _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }

            return await _client.GetAsync($"api/schedules/facilities/{Uri.EscapeDataString(facilityId)}?blocking=true", cancellationToken);
        }

        public async Task<HttpResponseMessage> SoftDeleteReportScheduleAsync(ClaimsPrincipal user, string reportScheduleId, CancellationToken cancellationToken)
        {
            if (!_authenticationSchemaConfig.Value.EnableAnonymousAccess)
            {
                var createLinkBearerToken = _scopeFactory.CreateScope().ServiceProvider.GetRequiredService<ICreateLinkBearerToken>();
                var token = await createLinkBearerToken.ExecuteAsync(user, 2, cancellationToken);
                _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }

            return await _client.DeleteAsync($"api/schedules/{Uri.EscapeDataString(reportScheduleId)}", cancellationToken);
        }

        public async Task<HttpResponseMessage> RestoreReportScheduleAsync(ClaimsPrincipal user, string reportScheduleId, CancellationToken cancellationToken)
        {
            if (!_authenticationSchemaConfig.Value.EnableAnonymousAccess)
            {
                var createLinkBearerToken = _scopeFactory.CreateScope().ServiceProvider.GetRequiredService<ICreateLinkBearerToken>();
                var token = await createLinkBearerToken.ExecuteAsync(user, 2, cancellationToken);
                _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }

            var request = new HttpRequestMessage(HttpMethod.Patch, $"api/schedules/{Uri.EscapeDataString(reportScheduleId)}/restore");
            return await _client.SendAsync(request, cancellationToken);
        }

        public async Task<HttpResponseMessage> SoftDeleteReportSchedulesAsync(ClaimsPrincipal user, string facilityId, CancellationToken cancellationToken)
        {
            if (!_authenticationSchemaConfig.Value.EnableAnonymousAccess)
            {
                var createLinkBearerToken = _scopeFactory.CreateScope().ServiceProvider.GetRequiredService<ICreateLinkBearerToken>();
                var token = await createLinkBearerToken.ExecuteAsync(user, 2, cancellationToken);
                _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }

            return await _client.PatchAsync($"api/schedules/facility/{Uri.EscapeDataString(facilityId)}/status?deleted=true", null, cancellationToken);
        }

        private void InitHttpClient()
        {
            //check if the service uri is set
            if (string.IsNullOrEmpty(_serviceRegistry.Value.ReportServiceUrl))
            {
                _logger.LogGatewayServiceUriException("Report", "Report service uri is not set");
                throw new ArgumentNullException("Report Service URL is missing.");
            }

            _client.BaseAddress = new Uri(_serviceRegistry.Value.ReportServiceUrl);
            _client.DefaultRequestHeaders.Accept.Clear();
            _client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }
    }
}
