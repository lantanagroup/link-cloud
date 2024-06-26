using LantanaGroup.Link.Shared.Application.Interfaces.Services.Security.Token;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using Link.Authorization.Infrastructure;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.Diagnostics;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace LantanaGroup.Link.Shared.Application.Services.Security.Token
{
    public class CreateUserToken : ICreateUserToken
    {
        private readonly ILogger<CreateUserToken> _logger;
        private readonly IOptions<LinkTokenServiceSettings> _linkTokenServiceConfig;

        public CreateUserToken(ILogger<CreateUserToken> logger, IOptions<LinkTokenServiceSettings> linkBearerServiceConfig)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _linkTokenServiceConfig = linkBearerServiceConfig ?? throw new ArgumentNullException(nameof(linkBearerServiceConfig));
        }

        public Task<string> ExecuteAsync(ClaimsPrincipal user, string key, int timespan)
        {
            if (string.IsNullOrEmpty(_linkTokenServiceConfig.Value.Authority))
            {
                throw new ArgumentNullException(nameof(_linkTokenServiceConfig.Value.Authority));
            }

            try
            {
                var credentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)), SecurityAlgorithms.HmacSha512);
                var token = new JwtSecurityToken(
                                    issuer: _linkTokenServiceConfig.Value.Authority,
                                    audience: LinkAuthorizationConstants.LinkBearerService.LinkBearerAudience,
                                    claims: user.Claims,
                                    expires: DateTime.Now.AddMinutes(timespan),
                                    signingCredentials: credentials
                                );

                var jwt = new JwtSecurityTokenHandler().WriteToken(token);

                _logger.LogInformation("Link token created for user {user}", user.Claims.First(c => c.Type == "sub").Value);
                if (_linkTokenServiceConfig.Value.LogToken)
                {
                    Activity.Current?.AddEvent(new("Token created.", tags: [
                        new KeyValuePair<string, object?>("token", jwt),
                    ]));
                }

                return Task.FromResult(jwt);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating user token: {message}", ex.Message);
                throw;
            }
        }
    }
}
