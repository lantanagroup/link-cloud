using LantanaGroup.Link.LinkAdmin.BFF.Application.Commands.Security;
using LantanaGroup.Link.LinkAdmin.BFF.Application.Models.Security;
using LantanaGroup.Link.LinkAdmin.BFF.Infrastructure.Logging;
using LantanaGroup.Link.LinkAdmin.BFF.Settings;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using Link.Authorization.Infrastructure;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using System.Security.Claims;

namespace LantanaGroup.Link.LinkAdmin.BFF.Infrastructure.Authentication
{
    public class LinkClaimsTransformer : IClaimsTransformation
    {
        private readonly ILogger<LinkClaimsTransformer> _logger;
        private readonly IGetLinkAccount _getLinkAccount;
        private readonly IDataProtectionProvider _dataProtectionProvider;
        private readonly IOptions<DataProtectionSettings> _dataProtectionOptions;

        private readonly ICacheService _cache;

        public LinkClaimsTransformer(ILogger<LinkClaimsTransformer> logger, IGetLinkAccount getLinkAccount, ICacheService cache, IDataProtectionProvider dataProtectionProvider, IOptions<DataProtectionSettings> dataProtectionOptions)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _getLinkAccount = getLinkAccount ?? throw new ArgumentNullException(nameof(getLinkAccount));
            _dataProtectionProvider = dataProtectionProvider ?? throw new ArgumentNullException(nameof(dataProtectionProvider));
            _dataProtectionOptions = dataProtectionOptions ?? throw new ArgumentNullException(nameof(dataProtectionOptions));
            _cache = cache;
        }

        public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
        {
            if (principal.Identity is not ClaimsIdentity identity) { return principal; }

            var accountId = identity.FindFirst(LinkAuthorizationConstants.LinkSystemClaims.Email)?.Value;

            if (accountId == null) { return principal; }

            Account? account;

            //if no cache providers have been registered, get the account from the account service
            {
                var userKey = "user:" + accountId;

                //check if account is in cache, if not get it from the account service
                var protector = _dataProtectionProvider.CreateProtector(LinkAdminConstants.LinkDataProtectors.LinkUser);
                string? cacheAccount = null;

                try
                {
                    cacheAccount = _cache.Get<string>(userKey);
                }
                catch (Exception ex)
                {
                    _logger.LogCacheException(userKey, ex.Message);
                }

                if (cacheAccount is not null)
                {
                    if (_dataProtectionOptions.Value.Enabled)
                    {
                        account = JsonConvert.DeserializeObject<Account>(protector.Unprotect(cacheAccount));
                    }
                    else
                    {
                        account = JsonConvert.DeserializeObject<Account>(cacheAccount);
                    }
                }
                else
                {
                    account = await _getLinkAccount.ExecuteAsync(principal, CancellationToken.None);
                }

                if (account is null) //if no account found, return an empty principal
                {
                    _logger.LogLinkServiceRequestWarning("Account not found for {accountId}", accountId);

                    var invalidIdentity = new ClaimsIdentity();
                    return new ClaimsPrincipal(invalidIdentity);
                };

                // Cache the account for 5 minutes if it is not already in the cache
                if (cacheAccount is null)
                {
                    try
                    {
                        if (_dataProtectionOptions.Value.Enabled)
                        {
                            _cache.Set<string>(userKey, protector.Protect(JsonConvert.SerializeObject(account)), TimeSpan.FromMinutes(5));
                        }
                        else
                        {
                            _cache.Set<string>(userKey, JsonConvert.SerializeObject(account), TimeSpan.FromMinutes(5));
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogCacheException(userKey, ex.Message);
                    }
                }
            }                              

            // Remove the existing 'sub' claim and replace with link account id
            var existingSubClaim = identity.FindFirst(LinkAuthorizationConstants.LinkSystemClaims.Subject);
            if (existingSubClaim != null)
            {
                identity.RemoveClaim(existingSubClaim);
                identity.AddClaim(new Claim(LinkAuthorizationConstants.LinkSystemClaims.Subject, account.Id));
            }

            //add user roles
            foreach (var role in account.Roles)
            {
                identity.AddClaim(new Claim(LinkAuthorizationConstants.LinkSystemClaims.Role, role));
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

            return principal;
            

        }
    }
}
