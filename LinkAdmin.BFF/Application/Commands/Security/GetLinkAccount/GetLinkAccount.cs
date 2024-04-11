using LantanaGroup.Link.LinkAdmin.BFF.Application.Models.Security;
using System.Net.Http.Headers;
using LantanaGroup.Link.LinkAdmin.BFF.Application.Models;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using LantanaGroup.Link.LinkAdmin.BFF.Settings;
using Link.Authorization.Infrastructure;
using LantanaGroup.Link.LinkAdmin.BFF.Infrastructure;
using System.Diagnostics;

namespace LantanaGroup.Link.LinkAdmin.BFF.Application.Commands.Security
{
    public class GetLinkAccount : IGetLinkAccount
    {
        private readonly ILogger<GetLinkAccount> _logger;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IOptions<LinkServiceDiscovery> _serviceRegistry;
        private readonly ICreateLinkBearerToken _createLinkBearerToken;

        public GetLinkAccount(ILogger<GetLinkAccount> logger, IHttpClientFactory httpClientFactory, IOptions<LinkServiceDiscovery> serviceRegistry, ICreateLinkBearerToken createLinkBearerToken)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
            _serviceRegistry = serviceRegistry ?? throw new ArgumentNullException(nameof(serviceRegistry));
            _createLinkBearerToken = createLinkBearerToken ?? throw new ArgumentNullException(nameof(createLinkBearerToken));
        }


        public async Task<Account?> ExecuteAsync(ClaimsPrincipal principal, CancellationToken cancellationToken)
        {
            //check if the principal is a claims identity
            if(principal.Identity is not ClaimsIdentity identity) { return null; }
            
            //get the account id from the claims
            var accountId = identity.FindFirst("email")?.Value;
            if (accountId == null) { return null; }

            //check if the account service uri is set
            if(string.IsNullOrEmpty(_serviceRegistry.Value.AccountServiceUri)) 
            {
                _logger.LogError("Account service uri is not set");
                return null; 
            }

            //create a http client
            var client = _httpClientFactory.CreateClient();
            client.BaseAddress = new Uri(_serviceRegistry.Value.AccountServiceUri);
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            using Activity? activity = ServiceActivitySource.Instance.StartActivity("Generate system account claims principle to request user account information");                    
            //create a bearer token
            //TODO: what should be the user making this request? The actuall user or a system account?
            //create a system account principal
            var claims = new List<Claim>
            {
                new Claim(LinkAuthorizationConstants.LinkSystemClaims.Email, "link.admin@nhsnlink.org"), //this needs to be configurable
                new Claim(LinkAuthorizationConstants.LinkSystemClaims.Role, "SystemAccount"),
                new Claim(LinkAuthorizationConstants.LinkSystemClaims.Role, LinkAuthorizationConstants.LinkUserClaims.LinkAdministartor)
            };

            var systemPrinciple = new ClaimsPrincipal(new ClaimsIdentity(claims));

            var bearerToken = await _createLinkBearerToken.ExecuteAsync(systemPrinciple, 2);
            if (string.IsNullOrEmpty(bearerToken))
            {
                _logger.LogError("Failed to create bearer token");
                return null;
            }


            //add the bearer token to the request
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);

            //send the request to the account service
            var response = await client.GetAsync($"api/account/email/{accountId}", cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Failed to get account from account service");
                return null;
            }

            //read the response
            var account = await response.Content.ReadFromJsonAsync<Account>();

            return account;

        }            
        
    }
}
