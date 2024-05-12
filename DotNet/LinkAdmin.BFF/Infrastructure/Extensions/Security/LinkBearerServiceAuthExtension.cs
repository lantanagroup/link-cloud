using LantanaGroup.Link.LinkAdmin.BFF.Settings;
using LantanaGroup.Link.Shared.Application.Interfaces.Services;
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
        //TODO: Add back data protection once key persience is implemented
        public static IServiceCollection AddLinkBearerServiceAuthentication(this IServiceCollection services, Action<LinkBearerServiceOptions>? options)
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
                    NameClaimType = linkBearerServiceOptions.NameClaimType,
                    RoleClaimType = linkBearerServiceOptions.RoleClaimType,
                    //avoid jwt confustion attacks (ie: circumvent token signature checking)
                    ValidTypes = linkBearerServiceOptions.ValidTypes,
                    IssuerSigningKeyResolver = (token, securityToken, kid, parameters) =>
                    {
                        var protector = services.BuildServiceProvider().GetRequiredService<IDataProtectionProvider>().CreateProtector(LinkAdminConstants.LinkDataProtectors.LinkSigningKey);
                        string bearerKey = string.Empty;

                        //check if bearer key is in cache, if not get it from the secret manager
                        var cache = services.BuildServiceProvider().GetRequiredService<IDistributedCache>();
                        string? cachedBearerKey = cache.GetString(LinkAuthorizationConstants.LinkBearerService.LinkBearerKeyName);

                        if (cachedBearerKey == null)
                        {
                            var secretManager = services.BuildServiceProvider().GetRequiredService<ISecretManager>();
                            var vaultResult = secretManager.GetSecretAsync(LinkAuthorizationConstants.LinkBearerService.LinkBearerKeyName, CancellationToken.None);

                            bearerKey = vaultResult.Result;

                            if (bearerKey == null)
                            {
                                throw new Exception("Bearer key not found");
                            }

                            //protect the bearer key and store it in the cache
                            cache.SetString(LinkAuthorizationConstants.LinkBearerService.LinkBearerKeyName, protector.Protect(bearerKey));
                            //cache.SetString(LinkAdminConstants.LinkBearerService.LinkBearerKeyName, bearerKey);
                        }
                        else
                        {
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
            public string? Authority { get; set; } = null!;
            public string? Audience { get; set; } = null!;
            public string NameClaimType { get; set; } = LinkAuthorizationConstants.LinkSystemClaims.Email;
            public string RoleClaimType { get; set; } = LinkAuthorizationConstants.LinkSystemClaims.Role;
            public string[]? ValidTypes { get; set; } = ["at+jwt", "JWT"];
        }
    }
}
