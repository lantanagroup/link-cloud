using LantanaGroup.Link.LinkAdmin.BFF.Application.Models.Security;
using System.Security.Claims;
using Link.Authorization.Infrastructure;
using LantanaGroup.Link.LinkAdmin.BFF.Infrastructure.Logging;
using LantanaGroup.Link.LinkAdmin.BFF.Application.Clients;

namespace LantanaGroup.Link.LinkAdmin.BFF.Application.Commands.Security
{
    public class GetLinkAccount : IGetLinkAccount
    {
        private readonly ILogger<GetLinkAccount> _logger;
        private readonly AccountService _accountService;

        public GetLinkAccount(ILogger<GetLinkAccount> logger, AccountService accountService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
           
            _accountService = accountService ?? throw new ArgumentNullException(nameof(accountService));
        }

        public async Task<Account?> ExecuteAsync(ClaimsPrincipal principal, CancellationToken cancellationToken)
        {
            //check if the principal is a claims identity
            if(principal.Identity is not ClaimsIdentity identity) { return null; }
            
            //get the account id from the claims
            var accountId = identity.FindFirst(LinkAuthorizationConstants.LinkSystemClaims.Email)?.Value;
            if (accountId == null) { return null; }          

            var response = await _accountService.GetAccountByEmail(accountId, cancellationToken);

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
