﻿using LantanaGroup.Link.LinkAdmin.BFF.Application.Interfaces.Infrastructure;
using LantanaGroup.Link.LinkAdmin.BFF.Infrastructure;
using LantanaGroup.Link.LinkAdmin.BFF.Infrastructure.Logging;
using LantanaGroup.Link.LinkAdmin.BFF.Settings;
using LantanaGroup.Link.Shared.Application.Interfaces.Services;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using Link.Authorization.Infrastructure;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Caching.Distributed;
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
        private readonly IDistributedCache _cache;
        private readonly ISecretManager _secretManager;
        private readonly IDataProtectionProvider _dataProtectionProvider;
        private readonly IOptions<DataProtectionSettings> _dataProtectionSettings;
        private readonly IOptions<LinkTokenServiceSettings> _linkTokenServiceConfig;
        private readonly ILinkAdminMetrics _metrics;

        public CreateLinkBearerToken(ILogger<CreateLinkBearerToken> logger, IDistributedCache cache, ISecretManager secretManager, IDataProtectionProvider dataProtectionProvider, IOptions<DataProtectionSettings> dataProtectionSettings, IOptions<LinkTokenServiceSettings> linkBearerServiceConfig, ILinkAdminMetrics metrics)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _secretManager = secretManager ?? throw new ArgumentNullException(nameof(secretManager));
            _dataProtectionProvider = dataProtectionProvider ?? throw new ArgumentNullException(nameof(dataProtectionProvider));
            _dataProtectionSettings = dataProtectionSettings ?? throw new ArgumentNullException(nameof(dataProtectionSettings));
            _linkTokenServiceConfig = linkBearerServiceConfig ?? throw new ArgumentNullException(nameof(linkBearerServiceConfig));
            _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));            
        }

        public async Task<string> ExecuteAsync(ClaimsPrincipal user, int timespan)
        {
            using Activity? activity = ServiceActivitySource.Instance.StartActivity("Generate Link Admin JWT");

            if(string.IsNullOrEmpty(_linkTokenServiceConfig.Value.Authority))
            {
                   throw new ArgumentNullException(nameof(_linkTokenServiceConfig.Value.Authority));
            }
            
            try
            {
                var protector = _dataProtectionProvider.CreateProtector(LinkAdminConstants.LinkDataProtectors.LinkSigningKey);
                string? bearerKey = _cache.GetString(LinkAuthorizationConstants.LinkBearerService.LinkBearerKeyName);

                if (bearerKey == null)
                {

                    bearerKey = await _secretManager.GetSecretAsync(LinkAuthorizationConstants.LinkBearerService.LinkBearerKeyName, CancellationToken.None);

                    if (_dataProtectionSettings.Value.Enabled)
                    {
                        _cache.SetString(LinkAuthorizationConstants.LinkBearerService.LinkBearerKeyName, protector.Protect(bearerKey));
                    }
                    else
                    {
                        _cache.SetString(LinkAuthorizationConstants.LinkBearerService.LinkBearerKeyName, bearerKey);
                    }                             
                }

                byte[] encodedKey = [];
                if (_dataProtectionSettings.Value.Enabled)
                {
                    encodedKey = Encoding.UTF8.GetBytes(protector.Unprotect(bearerKey));
                }
                else
                {
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
                Activity.Current?.RecordException(ex);
                throw;
            }
            
        }
    }
}
