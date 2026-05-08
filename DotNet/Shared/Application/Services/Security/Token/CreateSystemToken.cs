using LantanaGroup.Link.Shared.Application.Interfaces.Services.Security.Token;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using Link.Authorization.Infrastructure;
using Link.Authorization.Permissions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.Diagnostics;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace LantanaGroup.Link.Shared.Application.Services.Security.Token
{
    public class CreateSystemToken : ICreateSystemToken
    {
        private readonly ILogger<CreateSystemToken> _logger;
        private readonly IOptions<LinkTokenServiceSettings> _linkTokenServiceConfig;

        public CreateSystemToken(ILogger<CreateSystemToken> logger, IOptions<LinkTokenServiceSettings> linkBearerServiceConfig)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _linkTokenServiceConfig = linkBearerServiceConfig ?? throw new ArgumentNullException(nameof(linkBearerServiceConfig));
        }

        public Task<string> ExecuteAsync(string key, int timespan)
        {
            if (string.IsNullOrEmpty(_linkTokenServiceConfig.Value.Authority))
            {
                throw new ArgumentNullException(nameof(_linkTokenServiceConfig.Value.Authority));
            }

            try
            {
                //create a system account principal
                var claims = new List<Claim>
                {
                    new(LinkAuthorizationConstants.LinkSystemClaims.Email, _linkTokenServiceConfig.Value.LinkAdminEmail ?? string.Empty),
                    new(LinkAuthorizationConstants.LinkSystemClaims.Subject, LinkAuthorizationConstants.LinkUserClaims.LinkSystemAccount),
                    new(LinkAuthorizationConstants.LinkSystemClaims.Role, LinkAuthorizationConstants.LinkUserClaims.LinkAdministartor),
                    new(LinkAuthorizationConstants.LinkSystemClaims.LinkPermissions, nameof(LinkSystemPermissions.IsLinkAdmin))
                };               

                var credentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)), SecurityAlgorithms.HmacSha512);
                var token = new JwtSecurityToken(
                                    issuer: _linkTokenServiceConfig.Value.Authority,
                                    audience: LinkAuthorizationConstants.LinkBearerService.LinkBearerAudience,
                                    claims: claims,
                                    expires: DateTime.Now.AddMinutes(timespan),
                                    signingCredentials: credentials
                                );

                var jwt = new JwtSecurityTokenHandler().WriteToken(token);

                if (_linkTokenServiceConfig.Value.LogToken)
                {
                    Activity.Current?.AddEvent(new("Token generated.", tags: [
                        new KeyValuePair<string, object?>("token", jwt),
                    ]));
                }

                return Task.FromResult(jwt);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating system token: {message}", ex.Message);
                throw;
            }
        }
    }
}
