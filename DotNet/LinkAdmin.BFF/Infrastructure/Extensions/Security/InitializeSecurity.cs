using LantanaGroup.Link.LinkAdmin.BFF.Application.Models.Configuration;
using LantanaGroup.Link.LinkAdmin.BFF.Infrastructure.Authentication;
using LantanaGroup.Link.LinkAdmin.BFF.Settings;
using Link.Authorization.Infrastructure;
using Link.Authorization.Infrastructure.Extensions;
using Microsoft.AspNetCore.Authentication;

namespace LantanaGroup.Link.LinkAdmin.BFF.Infrastructure.Extensions.Security
{
    public static class InitializeSecurity
    {
        public static IServiceCollection AddLinkGatewaySecurity(this IServiceCollection services, IConfiguration configuration, Action<SecurityServiceOptions>? options = null)
        { 
            var securityServiceOptions = new SecurityServiceOptions();
            options?.Invoke(securityServiceOptions);

            services.AddTransient<IClaimsTransformation, LinkClaimsTransformer>();
            services.AddLinkAuthorizationHandlers();

            if (securityServiceOptions.Environment.IsDevelopment() && configuration.GetValue<bool>("Authentication:EnableAnonymousAccess"))
            {
                services.AddAuthentication(options =>
                {
                    options.RequireAuthenticatedSignIn = false;
                });

                //create anonymous access
                services.AddAuthorization(options =>
                {
                    options.AddPolicy("AuthenticatedUser", pb =>
                    {
                        pb.RequireAssertion(context => true);
                    });
                });

                return services;
            
            }                

            List<string> authSchemas = [LinkAdminConstants.AuthenticationSchemes.Cookie];

            var defaultChallengeScheme = configuration.GetValue<string>("Authentication:DefaultChallengeScheme");
            services.Configure<AuthenticationSchemaConfig>(options =>
            {
                options.DefaultScheme = LinkAdminConstants.AuthenticationSchemes.Cookie;

                if (string.IsNullOrEmpty(defaultChallengeScheme))
                    throw new NullReferenceException("DefaultChallengeScheme is required.");

                options.DefaultChallengeScheme = defaultChallengeScheme;
            });

            var authBuilder = services.AddAuthentication(options => {
                options.DefaultScheme = LinkAdminConstants.AuthenticationSchemes.Cookie;
                options.DefaultChallengeScheme = defaultChallengeScheme;
            });

            authBuilder.AddCookie(LinkAdminConstants.AuthenticationSchemes.Cookie, options =>
            {
                options.Cookie.Name = LinkAdminConstants.AuthenticationSchemes.Cookie;
                options.Cookie.SameSite = SameSiteMode.Strict;
                options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
                options.LoginPath = "/api/login";
                options.LogoutPath = "/api/logout";
            });

            //Add Oauth authorization scheme if enabled
            if (configuration.GetValue<bool>("Authentication:Schemas:Oauth2:Enabled"))
            {
                if (!LinkAdminConstants.AuthenticationSchemes.Oauth2.Equals(defaultChallengeScheme))
                    authSchemas.Add(LinkAdminConstants.AuthenticationSchemes.Oauth2);

                authBuilder.AddOAuthAuthentication(options =>
                {
                    options.Environment = securityServiceOptions.Environment;
                    options.ClientId = configuration.GetValue<string>("Authentication:Schemas:Oauth2:ClientId")!;
                    options.ClientSecret = configuration.GetValue<string>("Authentication:Schemas:Oauth2:ClientSecret")!;
                    options.AuthorizationEndpoint = configuration.GetValue<string>("Authentication:Schemas:Oauth2:Endpoints:Authorization")!;
                    options.TokenEndpoint = configuration.GetValue<string>("Authentication:Schemas:Oauth2:Endpoints:Token")!;
                    options.UserInformationEndpoint = configuration.GetValue<string>("Authentication:Schemas:Oauth2:Endpoints:UserInformation")!;
                    options.CallbackPath = configuration.GetValue<string>("Authentication:Schemas:Oauth2:CallbackPath");
                });
            }

            // Add OpenIdConnect authorization scheme if enabled
            if (configuration.GetValue<bool>("Authentication:Schemas:OpenIdConnect:Enabled"))
            {
                if (!LinkAdminConstants.AuthenticationSchemes.OpenIdConnect.Equals(defaultChallengeScheme))
                    authSchemas.Add(LinkAdminConstants.AuthenticationSchemes.OpenIdConnect);

                authBuilder.AddOpenIdConnectAuthentication(options =>
                {
                    options.Environment = securityServiceOptions.Environment;
                    options.Authority = configuration.GetValue<string>("Authentication:Schemas:OpenIdConnect:Authority")!;
                    options.ClientId = configuration.GetValue<string>("Authentication:Schemas:OpenIdConnect:ClientId")!;
                    options.ClientSecret = configuration.GetValue<string>("Authentication:Schemas:OpenIdConnect:ClientSecret")!;
                    options.NameClaimType = configuration.GetValue<string>("Authentication:Schemas:OpenIdConnect:NameClaimType");
                    options.RoleClaimType = configuration.GetValue<string>("Authentication:Schemas:OpenIdConnect:RoleClaimType");
                });
            }

            // Add JWT authorization scheme if enabled
            if (configuration.GetValue<bool>("Authentication:Schemas:Jwt:Enabled"))
            {
                if (!LinkAdminConstants.AuthenticationSchemes.JwtBearerToken.Equals(defaultChallengeScheme))
                    authSchemas.Add(LinkAdminConstants.AuthenticationSchemes.JwtBearerToken);

                authBuilder.AddJwTBearerAuthentication(options =>
                {
                    options.Environment = securityServiceOptions.Environment;
                    options.Authority = configuration.GetValue<string>("Authentication:Schemas:Jwt:Authority");
                    options.Audience = configuration.GetValue<string>("Authentication:Schemas:Jwt:Audience");
                    options.RequireHttpsMetadata = !securityServiceOptions.Environment.IsDevelopment() && configuration.GetValue<bool>("Authentication:Schemas:Jwt:RequireHttpsMetadata");
                    options.NameClaimType = configuration.GetValue<string>("Authentication:Schemas:Jwt:NameClaimType");
                    options.RoleClaimType = configuration.GetValue<string>("Authentication:Schemas:Jwt:RoleClaimType");

                });
            }

            // Add Link Bearer Token authorization schema
            if (!LinkAuthorizationConstants.AuthenticationSchemas.LinkBearerToken.Equals(defaultChallengeScheme))
                authSchemas.Add(LinkAuthorizationConstants.AuthenticationSchemas.LinkBearerToken);

            services.AddLinkBearerServiceAuthentication(options =>
            {
                options.Environment = securityServiceOptions.Environment;
                options.Authority = configuration.GetValue<string>("LinkBearerService:Authority") ?? LinkAuthorizationConstants.LinkBearerService.LinkBearerIssuer;
                options.Audience = LinkAuthorizationConstants.LinkBearerService.LinkBearerAudience;              
            });

            // Add Authorization
            services.AddAuthorization(builder =>
            {
                builder.AddPolicy("AuthenticatedUser", pb => {
                    pb.RequireAuthenticatedUser()
                        .AddAuthenticationSchemes([.. authSchemas]);
                });
            });

            // Configure CORS
            var corsConfig = configuration.GetSection(LinkAdminConstants.AppSettingsSectionNames.CORS).Get<CorsConfig>();
            if (corsConfig != null)
            {
                services.AddCorsService(options =>
                {
                    options.Environment = securityServiceOptions.Environment;
                    options.PolicyName = corsConfig.PolicyName;
                    options.AllowedHeaders = corsConfig.AllowedHeaders;
                    options.AllowedExposedHeaders = corsConfig.AllowedExposedHeaders;
                    options.AllowedMethods = corsConfig.AllowedMethods;
                    options.AllowedOrigins = corsConfig.AllowedOrigins;
                    options.AllowCredentials = corsConfig.AllowCredentials;
                });
            }
            else
            {
                throw new NullReferenceException("CORS Configuration was null.");
            }

            return services;
        
        }

        public class SecurityServiceOptions
        {
            public IWebHostEnvironment Environment { get; set; } = null!;
        }
    }
}
