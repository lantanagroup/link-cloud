using LantanaGroup.Link.Notification.Application.Interfaces;
using LantanaGroup.Link.Notification.Settings;
using Link.Authorization.Infrastructure;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Text;

namespace LantanaGroup.Link.Notification.Infrastructure.Extensions
{
    public static class LinkBearerAuthenticationExtension
    {
        public static IServiceCollection AddLinkBearerAuthentication(this IServiceCollection services, Action<LinkBearerServiceOptions>? options)
        {
            var linkBearerServiceOptions = new LinkBearerServiceOptions();
            options?.Invoke(linkBearerServiceOptions);

            JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();
            services.AddAuthentication().AddJwtBearer(NotificationConstants.LinkBearerService.Schema, options =>
            {
                options.Authority = NotificationConstants.LinkBearerService.LinkBearerIssuer;
                options.Audience = NotificationConstants.LinkBearerService.LinkBearerAudience;
                options.RequireHttpsMetadata = !linkBearerServiceOptions.Environment.IsDevelopment();
                options.MapInboundClaims = false;

                options.TokenValidationParameters = new()
                {
                    ValidIssuer = NotificationConstants.LinkBearerService.LinkBearerIssuer,
                    NameClaimType = linkBearerServiceOptions.NameClaimType,
                    RoleClaimType = linkBearerServiceOptions.RoleClaimType,
                    //avoid jwt confustion attacks (ie: circumvent token signature checking)
                    ValidTypes = linkBearerServiceOptions.ValidTypes,
                    IssuerSigningKeyResolver = (token, securityToken, kid, parameters) =>
                    {
                        //var protector = services.BuildServiceProvider().GetRequiredService<IDataProtectionProvider>().CreateProtector(LinkAdminConstants.LinkDataProtectors.LinkSigningKey);
                        string bearerKey = string.Empty;

                        //check if bearer key is in cache, if not get it from the secret manager
                        var cache = services.BuildServiceProvider().GetRequiredService<IDistributedCache>();
                        string? cachedBearerKey = cache.GetString(NotificationConstants.LinkBearerService.LinkBearerKeyName);

                        if (cachedBearerKey == null)
                        {
                            var secretManager = services.BuildServiceProvider().GetRequiredService<ISecretManager>();
                            var vaultResult = secretManager.GetSecretAsync(NotificationConstants.LinkBearerService.LinkBearerKeyName, CancellationToken.None);

                            bearerKey = vaultResult.Result;

                            if (bearerKey == null)
                            {
                                throw new Exception("Bearer key not found");
                            }

                            //protect the bearer key and store it in the cache
                            //cache.SetString(LinkAdminConstants.LinkBearerService.LinkBearerKeyName, protector.Protect(bearerKey));
                            cache.SetString(NotificationConstants.LinkBearerService.LinkBearerKeyName, bearerKey);
                        }
                        else
                        {
                            //TODO: Uncomment this line when the data protector is implemented                            
                            //bearerKey = protector.Unprotect(cachedBearerKey);
                            bearerKey = cachedBearerKey;
                        }

                        return new[] { new SymmetricSecurityKey(Encoding.UTF8.GetBytes(bearerKey)) };
                    }
                };
            });

            return services;

        }

        public class LinkBearerServiceOptions
        {
            public IWebHostEnvironment Environment { get; set; } = null!;
            public string NameClaimType { get; set; } = LinkAuthorizationConstants.LinkSystemClaims.Email;
            public string RoleClaimType { get; set; } = LinkAuthorizationConstants.LinkSystemClaims.Role;
            public string[]? ValidTypes { get; set; } = ["at+jwt", "JWT"];
        }

    }
}
