using LantanaGroup.Link.Shared.Application.Interfaces.Services;
using LantanaGroup.Link.Shared.Settings;
using Link.Authorization.Infrastructure;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Text;

namespace LantanaGroup.Link.LinkAdmin.BFF.Infrastructure.Extensions.Security
{
    public static class LinkBearerServiceAuthExtension
    {        
        public static IServiceCollection AddLinkBearerServiceAuthentication(this IServiceCollection services, Serilog.ILogger logger, Action<LinkBearerServiceOptions>? options)
        {
            var linkBearerServiceOptions = new LinkBearerServiceOptions();
            options?.Invoke(linkBearerServiceOptions);

            JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();
            services.AddAuthentication().AddJwtBearer(LinkAuthorizationConstants.AuthenticationSchemas.LinkBearerToken, options =>
            {
                options.Authority = linkBearerServiceOptions.Authority;
                options.Audience = linkBearerServiceOptions.Audience;
                options.RequireHttpsMetadata = !linkBearerServiceOptions.Environment.IsDevelopment();
                options.MapInboundClaims = false;

                options.TokenValidationParameters = new()
                {
                    ValidIssuer = linkBearerServiceOptions.Authority,
                    ValidAudience = linkBearerServiceOptions.Audience,
                    NameClaimType = linkBearerServiceOptions.NameClaimType,
                    RoleClaimType = linkBearerServiceOptions.RoleClaimType,
                    ValidTypes = linkBearerServiceOptions.ValidTypes, //avoid jwt confustion attacks (ie: circumvent token signature checking)

                    //configure validation of the token
                    ValidateAudience = linkBearerServiceOptions.ValidateToken,
                    ValidateIssuer = linkBearerServiceOptions.ValidateToken,
                    ValidateIssuerSigningKey = linkBearerServiceOptions.ValidateToken,

                    IssuerSigningKeyResolver = (token, securityToken, kid, parameters) =>
                    {
                        string bearerKey = string.Empty;

                        if (linkBearerServiceOptions.SigningKey is null)
                        {
                            var protector = services.BuildServiceProvider().GetRequiredService<IDataProtectionProvider>().CreateProtector(ConfigurationConstants.LinkDataProtectors.LinkSigningKey);

                            //check if bearer key is in cache, if not get it from the secret manager
                            var cache = services.BuildServiceProvider().GetRequiredService<IDistributedCache>();
                            string? cachedBearerKey = string.Empty;

                            try
                            {
                                cachedBearerKey = cache.GetString(LinkAuthorizationConstants.LinkBearerService.LinkBearerKeyName);
                            }
                            catch (Exception ex)
                            {
                                logger.Error(ex, "An exception occured while attempting to access cache {cacheKey}: {message}.", LinkAuthorizationConstants.LinkBearerService.LinkBearerKeyName, ex.Message);
                            }

                            if (string.IsNullOrEmpty(cachedBearerKey))
                            {
                                var secretManager = services.BuildServiceProvider().GetRequiredService<ISecretManager>();
                                var vaultResult = secretManager.GetSecretAsync(LinkAuthorizationConstants.LinkBearerService.LinkBearerKeyName, CancellationToken.None);

                                bearerKey = vaultResult.Result;

                                if (bearerKey == null)
                                {
                                    throw new Exception("Bearer key not found");
                                }

                                //protect the bearer key and store it in the cache
                                try 
                                {
                                    if (linkBearerServiceOptions.ProtectKey)
                                    {
                                        cache.SetString(LinkAuthorizationConstants.LinkBearerService.LinkBearerKeyName, protector.Protect(bearerKey));
                                    }
                                    else
                                    {
                                        cache.SetString(LinkAuthorizationConstants.LinkBearerService.LinkBearerKeyName, bearerKey);
                                    }
                                }
                                catch (Exception ex)
                                {
                                    logger.Error(ex, "An exception occured while attempting to store cache {cacheKey}: {message}.", LinkAuthorizationConstants.LinkBearerService.LinkBearerKeyName, ex.Message);
                                }                                
                            }
                            else
                            {
                                if (linkBearerServiceOptions.ProtectKey)
                                {
                                    bearerKey = protector.Unprotect(cachedBearerKey);
                                }
                                else
                                {
                                    bearerKey = cachedBearerKey;
                                }
                            }
                        }
                        else
                        {
                            bearerKey = linkBearerServiceOptions.SigningKey;
                        }

                        return [new SymmetricSecurityKey(Encoding.UTF8.GetBytes(bearerKey))];
                    }
                };
            });

            return services;

        }

        public class LinkBearerServiceOptions
        {
            public IWebHostEnvironment Environment { get; set; } = null!;
            public bool AllowAnonymous { get; set; } = false;
            public string? Authority { get; set; } = LinkAuthorizationConstants.LinkBearerService.LinkBearerIssuer;
            public string? Audience { get; set; } = LinkAuthorizationConstants.LinkBearerService.LinkBearerAudience;
            public string NameClaimType { get; set; } = LinkAuthorizationConstants.LinkSystemClaims.Email;
            public string RoleClaimType { get; set; } = LinkAuthorizationConstants.LinkSystemClaims.Role;
            public string LinkPermissionType { get; set; } = LinkAuthorizationConstants.LinkSystemClaims.LinkPermissions;
            public bool ProtectKey { get; set; } = true;
            public bool ValidateToken { get; set; } = true;
            public string[]? ValidTypes { get; set; } = ["at+jwt", "JWT"];
            public string? SigningKey { get; set; }
        }
    }
}
