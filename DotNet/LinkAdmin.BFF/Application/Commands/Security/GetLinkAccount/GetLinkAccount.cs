using LantanaGroup.Link.LinkAdmin.BFF.Application.Models.Security;
using System.Net.Http.Headers;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using LantanaGroup.Link.LinkAdmin.BFF.Infrastructure;
using System.Diagnostics;
using Link.Authorization.Infrastructure;
using LantanaGroup.Link.LinkAdmin.BFF.Infrastructure.Logging;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using Link.Authorization.Permissions;

namespace LantanaGroup.Link.LinkAdmin.BFF.Application.Commands.Security
{
    public class GetLinkAccount : IGetLinkAccount
    {
        private readonly ILogger<GetLinkAccount> _logger;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IOptions<ServiceRegistry> _serviceRegistry;
        private readonly IOptions<LinkTokenServiceSettings> _tokenServiceConfig;
        private readonly ICreateLinkBearerToken _createLinkBearerToken;

        public GetLinkAccount(ILogger<GetLinkAccount> logger, IHttpClientFactory httpClientFactory, IOptions<ServiceRegistry> serviceRegistry, IOptions<LinkTokenServiceSettings> tokenServiceConfig, ICreateLinkBearerToken createLinkBearerToken)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
            _serviceRegistry = serviceRegistry ?? throw new ArgumentNullException(nameof(serviceRegistry));
            _tokenServiceConfig = tokenServiceConfig ?? throw new ArgumentNullException(nameof(tokenServiceConfig));
            _createLinkBearerToken = createLinkBearerToken ?? throw new ArgumentNullException(nameof(createLinkBearerToken));            
        }

        public async Task<Account?> ExecuteAsync(ClaimsPrincipal principal, CancellationToken cancellationToken)
        {
            //check if the principal is a claims identity
            if(principal.Identity is not ClaimsIdentity identity) { return null; }
            
            //get the account id from the claims
            var accountId = identity.FindFirst(LinkAuthorizationConstants.LinkSystemClaims.Email)?.Value;
            if (accountId == null) { return null; }

            //check if the account service uri is set
            if(string.IsNullOrEmpty(_serviceRegistry.Value.AccountServiceUrl)) 
            {
                _logger.LogGatewayServiceUriException("Account", "Account service uri is not set");
                return null; 
            }

            //create a http client
            var client = _httpClientFactory.CreateClient();
            client.BaseAddress = new Uri(_serviceRegistry.Value.AccountServiceUrl);
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            using Activity? activity = ServiceActivitySource.Instance.StartActivity("Generate system account claims principle to request user account information");                    

            //create a system account principal
            var claims = new List<Claim>
            {
                new(LinkAuthorizationConstants.LinkSystemClaims.Email, _tokenServiceConfig.Value.LinkAdminEmail ?? string.Empty),
                new(LinkAuthorizationConstants.LinkSystemClaims.Subject, LinkAuthorizationConstants.LinkUserClaims.LinkSystemAccount),
                new(LinkAuthorizationConstants.LinkSystemClaims.Role, LinkAuthorizationConstants.LinkUserClaims.LinkAdministartor),
                new(LinkAuthorizationConstants.LinkSystemClaims.LinkPermissions, nameof(LinkSystemPermissions.IsLinkAdmin))
            };            

            var systemPrinciple = new ClaimsPrincipal(new ClaimsIdentity(claims));

            //create a bearer token for the system account
            var bearerToken = await _createLinkBearerToken.ExecuteAsync(systemPrinciple, 2);
            if (string.IsNullOrEmpty(bearerToken))
            {
                _logger.LogLinkAdminTokenGenerationException("Failed to create bearer token for user account retrieval");
                return null;
            }

            //add the bearer token to the request
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);

            //send the request to the account service
            var response = await client.GetAsync($"{_serviceRegistry.Value.AccountServiceUrl}/api/account/user/email/{accountId}", cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogLinkServiceRequestException("Account", $"Failed to retrieve account information for user.");
                return null;
            }

            //read the response
            var account = await response.Content.ReadFromJsonAsync<Account>(cancellationToken: cancellationToken);

            return account;

        }            
        
    }
}
