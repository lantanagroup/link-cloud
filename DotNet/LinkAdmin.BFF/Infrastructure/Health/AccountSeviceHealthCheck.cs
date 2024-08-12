using Hl7.Fhir.Language.Debugging;
using LantanaGroup.Link.LinkAdmin.BFF.Infrastructure.Logging;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
using System.Net.Http.Headers;
using System.Text.Json;

namespace LantanaGroup.Link.LinkAdmin.BFF.Infrastructure.Health
{
    public class AccountSeviceHealthCheck : IHealthCheck
    {
        private readonly ILogger<AccountSeviceHealthCheck> _logger;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IOptions<ServiceRegistry> _serviceRegistry;

        public AccountSeviceHealthCheck(ILogger<AccountSeviceHealthCheck> logger, IHttpClientFactory httpClientFactory, IOptions<ServiceRegistry> serviceRegistry)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
            _serviceRegistry = serviceRegistry ?? throw new ArgumentNullException(nameof(serviceRegistry));            
        }

        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            //check if the account service uri is set
            if (string.IsNullOrEmpty(_serviceRegistry.Value.AccountServiceUrl))
            {
                _logger.LogGatewayServiceUriException("Account", "Account service uri is not set");
                return new HealthCheckResult(HealthStatus.Unhealthy, description: "Account service uri is not set");
            }

            try
            {
                //create a http client
                var client = _httpClientFactory.CreateClient();
                client.BaseAddress = new Uri(_serviceRegistry.Value.AccountServiceUrl);
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                var response = await client.GetAsync($"{_serviceRegistry.Value.AccountServiceUrl}/health", cancellationToken);

                if (response.IsSuccessStatusCode)
                {

                    try
                    {
                        dynamic data = JObject.Parse(await response.Content.ReadAsStringAsync(cancellationToken: cancellationToken));

                        if (((string)data.status).Equals("Healthy", StringComparison.OrdinalIgnoreCase))
                        {
                            return HealthCheckResult.Healthy();
                        }
                        else
                        {
                            return new HealthCheckResult(HealthStatus.Unhealthy, description: "Account service is not healthy");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogGatewayServiceUriException("Account", ex.Message);
                        return new HealthCheckResult(HealthStatus.Unhealthy, description: "Failed to determine health status of the Account service.");
                    }
                }

                return new HealthCheckResult(HealthStatus.Unhealthy, description: "Failed to connect to account service");

            }
            catch (Exception ex)
            {
                _logger.LogGatewayServiceUriException("Account", ex.Message);
                return new HealthCheckResult(HealthStatus.Unhealthy, description: "Failed to determine health status of the Account service.");
            }            
        }
    }
}
