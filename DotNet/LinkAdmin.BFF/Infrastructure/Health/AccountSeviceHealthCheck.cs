﻿using LantanaGroup.Link.LinkAdmin.BFF.Application.Clients;
using LantanaGroup.Link.LinkAdmin.BFF.Infrastructure.Logging;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Newtonsoft.Json.Linq;

namespace LantanaGroup.Link.LinkAdmin.BFF.Infrastructure.Health
{
    public class AccountSeviceHealthCheck : IHealthCheck
    {
        private readonly ILogger<AccountSeviceHealthCheck> _logger;
        private readonly AccountService _accountService;

        public AccountSeviceHealthCheck(ILogger<AccountSeviceHealthCheck> logger, AccountService accountService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _accountService = accountService ?? throw new ArgumentNullException(nameof(accountService));
        }

        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {           
            try
            {             
                // make a request to the account service health check
                var response = await _accountService.ServiceHealthCheck(cancellationToken);
                
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
    }
}
