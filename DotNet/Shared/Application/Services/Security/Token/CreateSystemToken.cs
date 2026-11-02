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
        private readonly IActorEmailProvider? _actorEmailProvider;

        /// <param name="actorEmailProvider">
        /// LEGLINK-1359: optional, and null in every host that does not front real users. Dependency
        /// injection supplies the default when nothing is registered, so a host that adds no provider
        /// is unaffected by it.
        /// </param>
        public CreateSystemToken(ILogger<CreateSystemToken> logger, IOptions<LinkTokenServiceSettings> linkBearerServiceConfig,
            IActorEmailProvider? actorEmailProvider = null)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _linkTokenServiceConfig = linkBearerServiceConfig ?? throw new ArgumentNullException(nameof(linkBearerServiceConfig));
            _actorEmailProvider = actorEmailProvider;
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
                var claims = new List<Claim>();

                //LEGLINK-1359: the email claim is what a receiving service records as the actor, so
                //where the call was made on a person's behalf it names them rather than this
                //deployment's Link admin address. Only the value changes - subject, role and
                //permissions stay those of the system account, which is what authorization reads.
                AddActorEmailClaim(claims);

                claims.Add(new(LinkAuthorizationConstants.LinkSystemClaims.Subject, LinkAuthorizationConstants.LinkUserClaims.LinkSystemAccount));
                claims.Add(new(LinkAuthorizationConstants.LinkSystemClaims.Role, LinkAuthorizationConstants.LinkUserClaims.LinkAdministartor));
                claims.Add(new(LinkAuthorizationConstants.LinkSystemClaims.LinkPermissions, nameof(LinkSystemPermissions.IsLinkAdmin)));

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

        /// <summary>
        /// LEGLINK-1359: who this token is for.
        /// </summary>
        /// <remarks>
        /// Three cases, and the middle one is the only new behaviour:
        /// <list type="bullet">
        /// <item>No provider registered - every Link service and worker - so the configured admin
        /// address, exactly as before.</item>
        /// <item>A provider that names someone, so the call is recorded against them.</item>
        /// <item>A provider that names nobody, so no email claim at all. Falling back to the admin
        /// address here would attribute the call to a person who did nothing; leaving the claim off
        /// makes the receiving service record no actor, which is the truthful answer.</item>
        /// </list>
        /// </remarks>
        private void AddActorEmailClaim(List<Claim> claims)
        {
            if (_actorEmailProvider is null)
            {
                claims.Add(new(LinkAuthorizationConstants.LinkSystemClaims.Email, _linkTokenServiceConfig.Value.LinkAdminEmail ?? string.Empty));
                return;
            }

            var actorEmail = _actorEmailProvider.CurrentEmail;
            if (!string.IsNullOrWhiteSpace(actorEmail))
            {
                claims.Add(new(LinkAuthorizationConstants.LinkSystemClaims.Email, actorEmail));
            }
        }
    }
}
