using LantanaGroup.Link.LinkAdmin.BFF.Application.Interfaces.Services;
using LantanaGroup.Link.LinkAdmin.BFF.Infrastructure;
using LantanaGroup.Link.LinkAdmin.BFF.Settings;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.IdentityModel.Tokens;
using System.Diagnostics;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace LantanaGroup.Link.LinkAdmin.BFF.Application.Commands.Security
{
    public class CreateLinkBearerToken : ICreateLinkBearerToken
    {
        private readonly ILogger<CreateLinkBearerToken> _logger;
        private readonly IDistributedCache _cache;
        private readonly ISecretManager _secretManager;
        private readonly IDataProtectionProvider _dataProtectionProvider;

        public CreateLinkBearerToken(ILogger<CreateLinkBearerToken> logger, IDistributedCache cache, ISecretManager secretManager, IDataProtectionProvider dataProtectionProvider)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _secretManager = secretManager ?? throw new ArgumentNullException(nameof(secretManager));
            _dataProtectionProvider = dataProtectionProvider ?? throw new ArgumentNullException(nameof(dataProtectionProvider));
        }

        //TODO: Add back data protection once key persience is implemented
        public async Task<string> ExecuteAsync(ClaimsPrincipal user, int timespan)
        {
            using Activity? activity = ServiceActivitySource.Instance.StartActivity("Generate Link Admin JWT");
            
            try
            {
                var protector = _dataProtectionProvider.CreateProtector(LinkAdminConstants.LinkDataProtectors.LinkSigningKey);
                string? bearerKey = _cache.GetString(LinkAdminConstants.LinkBearerService.LinkBearerKeyName);

                if (bearerKey == null)
                {

                    bearerKey = await _secretManager.GetSecretAsync(LinkAdminConstants.LinkBearerService.LinkBearerKeyName, CancellationToken.None);
                    //_cache.SetString(LinkAdminConstants.LinkBearerService.LinkBearerKeyName, protector.Protect(bearerKey));
                    _cache.SetString(LinkAdminConstants.LinkBearerService.LinkBearerKeyName, bearerKey);
                }

                //var credentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(protector.Unprotect(bearerKey))), SecurityAlgorithms.HmacSha512Signature);
                var credentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(bearerKey)), SecurityAlgorithms.HmacSha512Signature);

                var token = new JwtSecurityToken(                                    
                                    issuer: LinkAdminConstants.LinkBearerService.LinkBearerIssuer,
                                    audience: LinkAdminConstants.LinkBearerService.LinkBearerAudience,
                                    claims: user.Claims,
                                    expires: DateTime.Now.AddMinutes(timespan),
                                    signingCredentials: credentials
                                );

                var jwt = new JwtSecurityTokenHandler().WriteToken(token);
                activity?.AddTag("link.token", jwt);

                return jwt;

            }
            catch (Exception)
            {
                Activity.Current?.SetStatus(ActivityStatusCode.Error);
                throw;
            }
            
        }
    }
}
