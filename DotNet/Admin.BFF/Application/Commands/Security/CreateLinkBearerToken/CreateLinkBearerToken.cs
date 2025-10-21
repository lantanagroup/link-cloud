using LantanaGroup.Link.LinkAdmin.BFF.Application.Interfaces.Infrastructure;
using LantanaGroup.Link.LinkAdmin.BFF.Infrastructure.Logging;
using LantanaGroup.Link.LinkAdmin.BFF.Settings;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Interfaces.Services;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using Link.Authorization.Infrastructure;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using OpenTelemetry.Trace;
using System.Diagnostics;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace LantanaGroup.Link.LinkAdmin.BFF.Application.Commands.Security
{
    public class CreateLinkBearerToken : ICreateLinkBearerToken
    {
        private readonly ILogger<CreateLinkBearerToken> _logger;
        private readonly ISecretManager _secretManager;
        private readonly IDataProtectionProvider _dataProtectionProvider;
        private readonly IOptions<DataProtectionSettings> _dataProtectionSettings;
        private readonly IOptions<LinkTokenServiceSettings> _linkTokenServiceConfig;
        private readonly ILinkAdminMetrics _metrics;

        private readonly ICacheService _cache;


        public CreateLinkBearerToken(ILogger<CreateLinkBearerToken> logger, ISecretManager secretManager, ICacheService cache, IDataProtectionProvider dataProtectionProvider, IOptions<DataProtectionSettings> dataProtectionSettings, IOptions<LinkTokenServiceSettings> linkBearerServiceConfig, ILinkAdminMetrics metrics)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _secretManager = secretManager ?? throw new ArgumentNullException(nameof(secretManager));
            _dataProtectionProvider = dataProtectionProvider ?? throw new ArgumentNullException(nameof(dataProtectionProvider));
            _dataProtectionSettings = dataProtectionSettings ?? throw new ArgumentNullException(nameof(dataProtectionSettings));
            _linkTokenServiceConfig = linkBearerServiceConfig ?? throw new ArgumentNullException(nameof(linkBearerServiceConfig));
            _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        }

        public async Task<string> ExecuteAsync(ClaimsPrincipal user, int timespan, CancellationToken cancellationToken = default)
        {
            using Activity? activity = ServiceActivitySource.Instance.StartActivity("Generate Link Admin JWT");

            if (string.IsNullOrEmpty(_linkTokenServiceConfig.Value.Authority))
            {
                throw new ArgumentNullException(nameof(_linkTokenServiceConfig.Value.Authority));
            }

            try
            {
                string bearerKey = string.Empty;
                var protector = _dataProtectionProvider.CreateProtector(LinkAdminConstants.LinkDataProtectors.LinkSigningKey);
                byte[] encodedKey = [];

                if (_linkTokenServiceConfig.Value.SigningKey is null)
                {
                    {
                        //attempt to get signing key from cache
                        try
                        {
                            bearerKey = await _cache.GetAsync<string>(LinkAuthorizationConstants.LinkBearerService.LinkBearerKeyName, cancellationToken) ?? string.Empty;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogCacheException(LinkAuthorizationConstants.LinkBearerService.LinkBearerKeyName, ex.Message);
                        }

                        //if key is not in cache, get it from the secret manager
                        if (string.IsNullOrEmpty(bearerKey))
                        {
                            bearerKey = await _secretManager.GetSecretAsync(LinkAuthorizationConstants.LinkBearerService.LinkBearerKeyName, cancellationToken);

                            //store signing key in cache
                            try
                            {
                                if (_dataProtectionSettings.Value.Enabled)
                                {
                                    await _cache.SetAsync(LinkAuthorizationConstants.LinkBearerService.LinkBearerKeyName, protector.Protect(bearerKey), TimeSpan.FromMinutes(5), cancellationToken: cancellationToken);
                                }
                                else
                                {
                                    await _cache.SetAsync(LinkAuthorizationConstants.LinkBearerService.LinkBearerKeyName, bearerKey, TimeSpan.FromMinutes(5), cancellationToken: cancellationToken);
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.LogCacheException(LinkAuthorizationConstants.LinkBearerService.LinkBearerKeyName, ex.Message);
                            }
                        }

                        if (_dataProtectionSettings.Value.Enabled)
                        {
                            encodedKey = Encoding.UTF8.GetBytes(protector.Unprotect(bearerKey));
                        }
                        else
                        {
                            encodedKey = Encoding.UTF8.GetBytes(bearerKey);
                        }
                    }
                }
                else
                {
                    bearerKey = _linkTokenServiceConfig.Value.SigningKey;
                    encodedKey = Encoding.UTF8.GetBytes(bearerKey);
                }

                var credentials = new SigningCredentials(new SymmetricSecurityKey(encodedKey), SecurityAlgorithms.HmacSha512);

                var token = new JwtSecurityToken(
                    issuer: _linkTokenServiceConfig.Value.Authority,
                    audience: LinkAuthorizationConstants.LinkBearerService.LinkBearerAudience,
                    claims: user.Claims,
                    expires: DateTime.Now.AddMinutes(timespan),
                    signingCredentials: credentials
                );

                var jwt = new JwtSecurityTokenHandler().WriteToken(token);

                var userId = user.Claims.First(c => c.Type == "sub").Value;
                _logger.LogLinkAdminTokenGenerated(DateTime.UtcNow, userId);
                _metrics.IncrementTokenGeneratedCounter([
                    new KeyValuePair<string, object?>("subject", userId),
                    new KeyValuePair<string, object?>("timespan", timespan)
                ]);

                if (_linkTokenServiceConfig.Value.LogToken)
                {
                    Activity.Current?.AddEvent(new("Token generated.", tags: [
                        new KeyValuePair<string, object?>("token", jwt),
                    ]));
                }

                return jwt;

            }
            catch (Exception ex)
            {
                Activity.Current?.SetStatus(ActivityStatusCode.Error);
                Activity.Current?.AddException(ex);
                throw;
            }

        }
    }
}
