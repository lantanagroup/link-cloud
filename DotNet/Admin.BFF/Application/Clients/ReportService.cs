using LantanaGroup.Link.LinkAdmin.BFF.Infrastructure.Logging;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using LantanaGroup.Link.LinkAdmin.BFF.Application.Commands.Security;
using LantanaGroup.Link.LinkAdmin.BFF.Application.Models.Configuration;
using LantanaGroup.Link.LinkAdmin.BFF.Application.Models.Health;
using Link.Authorization.Infrastructure;
using Link.Authorization.Permissions;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace LantanaGroup.Link.LinkAdmin.BFF.Application.Clients
{
    public class ReportService
    {
        private readonly ILogger<ReportService> _logger;
        private readonly HttpClient _client;
        private readonly IOptions<ServiceRegistry> _serviceRegistry;
        private readonly IOptions<AuthenticationSchemaConfig> _authenticationSchemaConfig;
        private readonly IOptions<LinkTokenServiceSettings> _tokenServiceConfig;
        private readonly ICreateLinkBearerToken _createLinkBearerToken;
        
        private readonly ClaimsPrincipal _systemPrincipal;

        public ReportService(ILogger<ReportService> logger, HttpClient client, IOptions<ServiceRegistry> serviceRegistry, IOptions<AuthenticationSchemaConfig> authenticationSchemaConfig, IOptions<LinkTokenServiceSettings> tokenServiceConfig, ICreateLinkBearerToken createLinkBearerToken)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _serviceRegistry = serviceRegistry ?? throw new ArgumentNullException(nameof(serviceRegistry));
            _authenticationSchemaConfig = authenticationSchemaConfig ?? throw new ArgumentNullException(nameof(authenticationSchemaConfig));
            _tokenServiceConfig = tokenServiceConfig ?? throw new ArgumentNullException(nameof(tokenServiceConfig));
            _createLinkBearerToken = createLinkBearerToken ?? throw new ArgumentNullException(nameof(createLinkBearerToken));

            _systemPrincipal = CreateSystemAccountPrincipal();
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
                if (healthResult is not null) healthResult.Service = "Report";

                return healthResult;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Report service health check failed");
                return new LinkServiceHealthReport { Service = "Report", Status = HealthStatus.Unhealthy };
            }
        }
        
        public async Task<HttpResponseMessage> ReportSummaryList(string? facilityId, int pageNumber, int pageSize, CancellationToken cancellationToken)
        {
            // HTTP GET
            try
            {
                if (!_authenticationSchemaConfig.Value.EnableAnonymousAccess)
                {
                    //create a bearer token for the system account
                    var bearerToken = await _createLinkBearerToken.ExecuteAsync(_systemPrincipal, 2);
                    if (string.IsNullOrEmpty(bearerToken))
                    {
                        _logger.LogLinkAdminTokenGenerationException("Failed to create bearer token for user account retrieval");
                        return null;
                    }

                    //add the bearer token to the request
                    _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
                }
                
                var queryStringBuilder = new StringBuilder("?");
                if(facilityId is not null)
                {
                    queryStringBuilder.Append($"facilityId={facilityId}&");
                }
            
                queryStringBuilder.Append($"pageNumber={pageNumber}&pageSize={pageSize}");
                
                var response = await _client.GetAsync($"api/Report/summaries{queryStringBuilder}", cancellationToken);
                
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Report service health check failed");
                throw new Exception("Report summary list request failed");
            }
        }
        
        //TODO: centralize this along with the current implementation in account service client
        private ClaimsPrincipal CreateSystemAccountPrincipal()
        {
            var claims = new List<Claim>
            {
                new(LinkAuthorizationConstants.LinkSystemClaims.Email, _tokenServiceConfig.Value.LinkAdminEmail ?? string.Empty),
                new(LinkAuthorizationConstants.LinkSystemClaims.Subject, LinkAuthorizationConstants.LinkUserClaims.LinkSystemAccount),
                new(LinkAuthorizationConstants.LinkSystemClaims.Role, LinkAuthorizationConstants.LinkUserClaims.LinkAdministartor),
                new(LinkAuthorizationConstants.LinkSystemClaims.LinkPermissions, nameof(LinkSystemPermissions.IsLinkAdmin))
            };
           
            return new ClaimsPrincipal(new ClaimsIdentity(claims));
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
