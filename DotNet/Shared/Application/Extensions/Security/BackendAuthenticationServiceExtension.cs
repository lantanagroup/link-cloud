using LantanaGroup.Link.Shared.Application.Interfaces.Services;
using LantanaGroup.Link.Shared.Application.Interfaces.Services.Security.Token;
using LantanaGroup.Link.Shared.Application.Services.Security.Token;
using LantanaGroup.Link.Shared.Settings;
using Link.Authorization.Infrastructure;
using Link.Authorization.Policies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Text;

namespace LantanaGroup.Link.Shared.Application.Extensions.Security
{
    public static class BackendAuthenticationServiceExtension
    {

        public static IServiceCollection AddLinkBearerServiceAuthentication(this IServiceCollection services, Action<LinkBearerServiceOptions>? options)
        {
            var linkBearerServiceOptions = new LinkBearerServiceOptions();
            options?.Invoke(linkBearerServiceOptions);

            //add token commands            
            services.AddOptions<LinkBearerServiceOptions>().Configure(options);
            services.AddTransient<ICreateSystemToken, CreateSystemToken>();
            services.AddTransient<ICreateUserToken, CreateUserToken>();

            if (linkBearerServiceOptions.AllowAnonymous)
            {             
                services.AddAuthorizationBuilder()
                   .AddPolicy("CanViewAccounts", pb => { pb.RequireAssertion(context => true); })
                   .AddPolicy("CanAdministerAccounts", pb => { pb.RequireAssertion(context => true); })
                   .AddPolicy("FacilityAccess", pb => { pb.RequireAssertion(context => true); })
                   .AddPolicy("CanViewLogs", pb => { pb.RequireAssertion(context => true); })
                   .AddPolicy("CanViewNotifications", pb => { pb.RequireAssertion(context => true); })
                   .AddPolicy("CanViewTenantConfigurations", pb => { pb.RequireAssertion(context => true); })
                   .AddPolicy("CanEditTenantConfigurations", pb => { pb.RequireAssertion(context => true); })
                   .AddPolicy("CanViewResources", pb => { pb.RequireAssertion(context => true); })
                   .AddPolicy("CanViewReports", pb => { pb.RequireAssertion(context => true); })
                   .AddPolicy("CanGenerateReports", pb => { pb.RequireAssertion(context => true); })
                   .AddPolicy("CanGenerateEvents", pb => { pb.RequireAssertion(context => true); })
                   .AddPolicy("IsLinkAdmin", pb => { pb.RequireAssertion(context => true); });                    

                return services;
            }


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
                                if (linkBearerServiceOptions.ProtectKey)
                                {
                                    cache.SetString(LinkAuthorizationConstants.LinkBearerService.LinkBearerKeyName, protector.Protect(bearerKey));
                                }
                                else
                                {
                                    cache.SetString(LinkAuthorizationConstants.LinkBearerService.LinkBearerKeyName, bearerKey);
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

                        return new[] { new SymmetricSecurityKey(Encoding.UTF8.GetBytes(bearerKey)) };
                    }
                };
            });

            services.AddAuthorizationBuilder()
                .AddPolicy("CanViewAccounts", AuthorizationPolicies.CanViewAccounts())
                .AddPolicy("CanAdministerAccounts", AuthorizationPolicies.CanAdministerAccounts())
                .AddPolicy("FacilityAccess", AuthorizationPolicies.FacilityAccess())
                .AddPolicy("CanViewLogs", AuthorizationPolicies.CanViewLogs())
                .AddPolicy("CanViewNotifications", AuthorizationPolicies.CanViewNotifications())
                .AddPolicy("CanViewTenantConfigurations", AuthorizationPolicies.CanViewTenantConfigurations())
                .AddPolicy("CanEditTenantConfigurations", AuthorizationPolicies.CanEditTenantConfigurations())
                .AddPolicy("CanViewResources", AuthorizationPolicies.CanViewResources())
                .AddPolicy("CanViewReports", AuthorizationPolicies.CanViewReports())
                .AddPolicy("CanGenerateReports", AuthorizationPolicies.CanGenerateReports())
                .AddPolicy("CanGenerateEvents", AuthorizationPolicies.CanGenerateEvents())
                .AddPolicy("IsLinkAdmin", AuthorizationPolicies.IsLinkAdmin());

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
