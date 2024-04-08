using LantanaGroup.Link.LinkAdmin.BFF.Application.Commands.Security;
using LantanaGroup.Link.LinkAdmin.BFF.Application.Models;
using LantanaGroup.Link.LinkAdmin.BFF.Application.Models.Security;
using LantanaGroup.Link.LinkAdmin.BFF.Settings;
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
        private readonly IOptions<LinkServiceDiscovery> _serviceDiscovery;
        private readonly IGetLinkAccount _getLinkAccount;
        private readonly IDistributedCache _cache;
        private readonly IDataProtectionProvider _dataProtectionProvider;

        public LinkClaimsTransformer(ILogger<LinkClaimsTransformer> logger, IOptions<LinkServiceDiscovery> serviceDiscovery, IGetLinkAccount getLinkAccount, IDistributedCache cache, IDataProtectionProvider dataProtectionProvider)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _serviceDiscovery = serviceDiscovery ?? throw new ArgumentNullException(nameof(serviceDiscovery));
            _getLinkAccount = getLinkAccount ?? throw new ArgumentNullException(nameof(getLinkAccount));
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _dataProtectionProvider = dataProtectionProvider ?? throw new ArgumentNullException(nameof(dataProtectionProvider));
        }

        public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
        {
            if (principal.Identity is not ClaimsIdentity identity) { return principal; }

            var accountId = identity.FindFirst("email")?.Value;

            if (accountId == null) { return principal; }

            var userKey = "user:" + accountId;

            //check if account is in cache, if not get it from the account service
            var protector = _dataProtectionProvider.CreateProtector(LinkAdminConstants.LinkDataProtectors.LinkUser);
            var cacheAccount = _cache.GetString(userKey);            
            
            Account? account = cacheAccount is not null ? JsonConvert.DeserializeObject<Account>(protector.Unprotect(cacheAccount)) :
                await _getLinkAccount.ExecuteAsync(principal, CancellationToken.None);           

            if (account is null) //if no account found, return an empty principal
            {
                _logger.LogWarning("Account not found for {accountId}", accountId);
                var invalidIdentity = new ClaimsIdentity();
                return new ClaimsPrincipal(invalidIdentity);              
            };

            // Cache the account for 5 minutes if it is not already in the cache
            if(cacheAccount is null)
            {
                
                _cache.SetString(userKey, protector.Protect(JsonConvert.SerializeObject(account)), new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5) });
            }                  

            // Remove the existing 'sub' claim and replace with link account id
            var existingSubClaim = identity.FindFirst("sub");
            if (existingSubClaim != null)
            {
                identity.RemoveClaim(existingSubClaim);
                identity.AddClaim(new Claim("sub", account.Id));
            }                   

            // Add the account role claims
            foreach (var role in account.Roles)
            {
                identity.AddClaim(new Claim("role", role.Name));
            }

            // Add facility ids associated with the user
            foreach (var facility in account.FacilityIds)
            {
                identity.AddClaim(new Claim("facilities", facility));
            }

            return principal;
            

        }
    }
}
