using LantanaGroup.Link.LinkAdmin.BFF.Application.Interfaces;
using LantanaGroup.Link.LinkAdmin.BFF.Settings;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Text;

namespace LantanaGroup.Link.LinkAdmin.BFF.Infrastructure.Extensions
{
    public static class LinkBearerServiceAuthExtension
    {
        public static IServiceCollection AddLinkBearerServiceAuthentication(this IServiceCollection services, Action<LinkBearerServiceOptions>? options)
        {
            var linkBearerServiceOptions = new LinkBearerServiceOptions();
            options?.Invoke(linkBearerServiceOptions);

            JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();
            services.AddAuthentication().AddJwtBearer(LinkAdminConstants.AuthenticationSchemes.LinkBearerToken, options =>
            {                               
                options.Authority = linkBearerServiceOptions.Authority;
                options.Audience = linkBearerServiceOptions.Audience;
                options.RequireHttpsMetadata = !linkBearerServiceOptions.Environment.IsDevelopment();

                options.TokenValidationParameters = new()
                {
                    ValidIssuer = linkBearerServiceOptions.Authority,
                    NameClaimType = linkBearerServiceOptions.NameClaimType,
                    RoleClaimType = linkBearerServiceOptions.RoleClaimType,
                    //avoid jwt confustion attacks (ie: circumvent token signature checking)
                    ValidTypes = linkBearerServiceOptions.ValidTypes,
                    IssuerSigningKeyResolver = (token, securityToken, kid, parameters) =>
                    {                      
                        string bearerKey = string.Empty;

                        //check if bearer key is in cache, if not get it from the secret manager
                        var cache = services.BuildServiceProvider().GetRequiredService<IDistributedCache>();
                        string? cachedBearerKey = cache.GetString(LinkAdminConstants.LinkBearerService.LinkBearerKeyName);

                        if (cachedBearerKey == null)
                        {
                            var secretManager = services.BuildServiceProvider().GetRequiredService<ISecretManager>();
                            var vaultResult = secretManager.GetSecretAsync(LinkAdminConstants.LinkBearerService.LinkBearerKeyName, CancellationToken.None);

                            bearerKey = vaultResult.Result;

                            if (bearerKey == null)
                            {
                                throw new Exception("Bearer key not found");
                            }
                        }
                        else
                        {
                            var protector = services.BuildServiceProvider().GetRequiredService<IDataProtectionProvider>().CreateProtector(LinkAdminConstants.LinkDataProtectors.LinkSigningKey);
                            bearerKey = protector.Unprotect(cachedBearerKey);
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
            public string NameClaimType { get; set; } = "email";
            public string RoleClaimType { get; set; } = "roles";
            public string[]? ValidTypes { get; set; } = ["at+jwt", "JWT"];
        }
    }
}
