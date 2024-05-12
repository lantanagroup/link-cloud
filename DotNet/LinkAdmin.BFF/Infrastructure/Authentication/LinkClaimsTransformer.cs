using LantanaGroup.Link.LinkAdmin.BFF.Application.Commands.Security;
using LantanaGroup.Link.LinkAdmin.BFF.Application.Models.Configuration;
using LantanaGroup.Link.LinkAdmin.BFF.Application.Models.Security;
using LantanaGroup.Link.LinkAdmin.BFF.Infrastructure.Logging;
using LantanaGroup.Link.LinkAdmin.BFF.Settings;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using Link.Authorization.Infrastructure;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using System.Security.Claims;

namespace LantanaGroup.Link.LinkAdmin.BFF.Infrastructure.Authentication
{
    public class LinkClaimsTransformer : IClaimsTransformation
    {
        private readonly ILogger<LinkClaimsTransformer> _logger;
        private readonly IOptions<ServiceRegistry> _serviceRegistry;
        private readonly IGetLinkAccount _getLinkAccount;
        private readonly IDistributedCache _cache;
        private readonly IDataProtectionProvider _dataProtectionProvider;

        public LinkClaimsTransformer(ILogger<LinkClaimsTransformer> logger, IOptions<ServiceRegistry> serviceRegistry, IGetLinkAccount getLinkAccount, IDistributedCache cache, IDataProtectionProvider dataProtectionProvider)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _serviceRegistry = serviceRegistry ?? throw new ArgumentNullException(nameof(serviceRegistry));
            _getLinkAccount = getLinkAccount ?? throw new ArgumentNullException(nameof(getLinkAccount));
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _dataProtectionProvider = dataProtectionProvider ?? throw new ArgumentNullException(nameof(dataProtectionProvider));
        }

        public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
        {
            if (principal.Identity is not ClaimsIdentity identity) { return principal; }

            var accountId = identity.FindFirst(LinkAuthorizationConstants.LinkSystemClaims.Email)?.Value;

            if (accountId == null) { return principal; }

            var userKey = "user:" + accountId;

            //check if account is in cache, if not get it from the account service
            var protector = _dataProtectionProvider.CreateProtector(LinkAdminConstants.LinkDataProtectors.LinkUser);
            var cacheAccount = _cache.GetString(userKey);            
            
            Account? account = cacheAccount is not null ? JsonConvert.DeserializeObject<Account>(protector.Unprotect(cacheAccount)) :
                await _getLinkAccount.ExecuteAsync(principal, CancellationToken.None);           

            if (account is null) //if no account found, return an empty principal
            {
                _logger.LogLinkServiceRequestWarning("Account not found for {accountId}", accountId);
               
                var invalidIdentity = new ClaimsIdentity();
                return new ClaimsPrincipal(invalidIdentity);              
            };

            // Cache the account for 5 minutes if it is not already in the cache
            if(cacheAccount is null)
            {                
                _cache.SetString(userKey, protector.Protect(JsonConvert.SerializeObject(account)), new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5) });
            }                  

            // Remove the existing 'sub' claim and replace with link account id
            var existingSubClaim = identity.FindFirst(LinkAuthorizationConstants.LinkSystemClaims.Subject);
            if (existingSubClaim != null)
            {
                identity.RemoveClaim(existingSubClaim);
                identity.AddClaim(new Claim(LinkAuthorizationConstants.LinkSystemClaims.Subject, account.Id));
            }

            //add user specified claims
            foreach (var claim in account.UserClaims)
            {
                identity.AddClaim(new Claim(LinkAuthorizationConstants.LinkSystemClaims.LinkPermissions, claim));
            }

            // Add role claims
            var uniqueRoleClaims = account.RoleClaims.Except(account.UserClaims);
            foreach(var claim in uniqueRoleClaims)
            {
                identity.AddClaim(new Claim(LinkAuthorizationConstants.LinkSystemClaims.LinkPermissions, claim));
            }           

            // Add facility ids associated with the user
            
            foreach (var facility in account.Facilities)
            {
                identity.AddClaim(new Claim(LinkAuthorizationConstants.LinkSystemClaims.Facility, facility));
            }

            return principal;
            

        }
    }
}
