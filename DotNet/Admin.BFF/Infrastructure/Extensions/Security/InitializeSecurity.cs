using LantanaGroup.Link.LinkAdmin.BFF.Application.Models.Configuration;
using LantanaGroup.Link.LinkAdmin.BFF.Infrastructure.Authentication;
using LantanaGroup.Link.LinkAdmin.BFF.Settings;
using Link.Authorization.Infrastructure;
using Link.Authorization.Infrastructure.Extensions;
using Link.Authorization.Policies;
using Microsoft.AspNetCore.Authentication;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace LantanaGroup.Link.LinkAdmin.BFF.Infrastructure.Extensions.Security
{
    public static class InitializeSecurity
    {
        public static IServiceCollection AddLinkGatewaySecurity(this IServiceCollection services, IConfiguration configuration, Serilog.ILogger logger, Action<SecurityServiceOptions>? options = null)
        { 
            var securityServiceOptions = new SecurityServiceOptions();
            options?.Invoke(securityServiceOptions);

            services.AddTransient<IClaimsTransformation, LinkClaimsTransformer>();
            services.AddLinkAuthorizationHandlers();

            logger.Information("Initializing authentication schemas.");
            List<string> authSchemas = [LinkAdminConstants.AuthenticationSchemes.Cookie];

            var defaultChallengeScheme = configuration.GetValue<string>("Authentication:DefaultChallengeScheme");
            services.Configure<AuthenticationSchemaConfig>(options =>
            {
                options.DefaultScheme = LinkAdminConstants.AuthenticationSchemes.Cookie;

                if (string.IsNullOrEmpty(defaultChallengeScheme))
                    throw new NullReferenceException("DefaultChallengeScheme is required.");

                options.DefaultChallengeScheme = defaultChallengeScheme;

                options.EnableAnonymousAccess = configuration.GetValue<bool>("Authentication:EnableAnonymousAccess");

            });

            if (securityServiceOptions.Environment.IsDevelopment() && configuration.GetValue<bool>("Authentication:EnableAnonymousAccess"))
            {
                services.AddAuthentication(options =>
                {
                    options.RequireAuthenticatedSignIn = false;
                });

                //create anonymous access
                services.AddAuthorization(options =>
                {
                    options.AddPolicy(LinkAuthorizationConstants.LinkBearerService.AuthenticatedUserPolicyName, pb =>
                    {
                        pb.RequireAssertion(context => true);
                    });

                    options.AddPolicy(PolicyNames.IsLinkAdmin, pb => { pb.RequireAssertion(context => true); });                    
                });

                return services;
            
            }              

            var authBuilder = services.AddAuthentication(options => {
                options.DefaultScheme = LinkAdminConstants.AuthenticationSchemes.Cookie;
                options.DefaultChallengeScheme = defaultChallengeScheme;
            });

            logger.Information("Set Default Authentication Scheme: {DefaultScheme}", LinkAdminConstants.AuthenticationSchemes.Cookie);
            logger.Information("Set Default Authentication Challenge Scheme: {DefaultChallengeScheme}", defaultChallengeScheme);

            authBuilder.AddCookie(LinkAdminConstants.AuthenticationSchemes.Cookie, options =>
            {
                logger.Information("Registering Cookie Authentication Scheme: {Scheme}", LinkAdminConstants.AuthenticationSchemes.Cookie);

                options.Cookie.Name = LinkAdminConstants.AuthenticationSchemes.Cookie;
                options.Cookie.HttpOnly = configuration.GetValue<bool>("Authentication:Schemas:Cookie:HttpOnly");
                options.Cookie.SecurePolicy = securityServiceOptions.Environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;                
                options.Cookie.Path = configuration.GetValue<string>("Authentication:Schemas:Cookie:Path");                
                options.Cookie.SameSite = SameSiteMode.Strict;
                options.LoginPath = "/api/login";
                options.LogoutPath = "/api/logout";

                if (!string.IsNullOrEmpty(configuration.GetValue<string>("Authentication:Schemas:Cookie:Domain")))
                {
                    options.Cookie.Domain = configuration.GetValue<string>("Authentication:Schemas:Cookie:Domain");
                }

                options.Events.OnSigningIn = context =>
                {
                    //get claims principal
                    if (context.Principal?.Identity is not ClaimsIdentity claimsIdentity)
                    {
                        return Task.CompletedTask;
                    }
                    // Define the claim types to keep
                    var allowedClaims = new HashSet<string> {
                        JwtRegisteredClaimNames.FamilyName,
                        JwtRegisteredClaimNames.GivenName,
                        LinkAuthorizationConstants.LinkSystemClaims.Email,
                        LinkAuthorizationConstants.LinkSystemClaims.Subject,
                        LinkAuthorizationConstants.LinkSystemClaims.Role,
                        LinkAuthorizationConstants.LinkSystemClaims.LinkPermissions
                    };
                    //Define all claims that should be removed
                    var claimsToRemove = claimsIdentity.Claims
                        .Where(claim => !allowedClaims.Contains(claim.Type))
                        .ToList();
                    //remove uneeeded claims
                    foreach (var claim in claimsToRemove)
                    {
                        claimsIdentity.RemoveClaim(claim);
                    }
                    logger.Debug("User {User} is signing in.", claimsIdentity.Name);
                    return Task.CompletedTask;
                };

            });

            logger.Debug("Registering Cookie Authentication Scheme: {Scheme} with the following settings: ", LinkAdminConstants.AuthenticationSchemes.Cookie);
            if (!string.IsNullOrEmpty(configuration.GetValue<string>("Authentication:Schemas:Cookie:Domain")))
            {
                logger.Debug("Cookie Domain: {Domain}", configuration.GetValue<string>("Authentication:Schemas:Cookie:Domain"));
            }
            logger.Debug("Cookie Path: {Path}", configuration.GetValue<string>("Authentication:Schemas:Cookie:Path"));
            logger.Debug("Cookie HttpOnly: {HttpOnly}", configuration.GetValue<bool>("Authentication:Schemas:Cookie:HttpOnly"));
            logger.Debug("Cookie SecurePolicy: {SecurePolicy}", nameof(CookieSecurePolicy.SameAsRequest));
            logger.Debug("Cookie SameSite: {SameSite}", nameof(SameSiteMode.Strict));

            //Add Oauth authorization scheme if enabled
            if (configuration.GetValue<bool>("Authentication:Schemas:Oauth2:Enabled"))
            {
                logger.Information("Registering OAuth2 authentication scheme: {Scheme}", LinkAdminConstants.AuthenticationSchemes.Oauth2);
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

                logger.Debug("Set OAuth2 Authentication Scheme: {Scheme} with the following settings: ", LinkAdminConstants.AuthenticationSchemes.Oauth2);
                logger.Debug("Client Id: {ClientId}", configuration.GetValue<string>("Authentication:Schemas:Oauth2:ClientId"));
                logger.Debug("Authorization Endpoint: {AuthorizationEndpoint}", configuration.GetValue<string>("Authentication:Schemas:Oauth2:Endpoints:Authorization"));
                logger.Debug("Token Endpoint: {TokenEndpoint}", configuration.GetValue<string>("Authentication:Schemas:Oauth2:Endpoints:Token"));
                logger.Debug("User Information Endpoint: {UserInformationEndpoint}", configuration.GetValue<string>("Authentication:Schemas:Oauth2:Endpoints:UserInformation"));
                logger.Debug("Callback Path: {CallbackPath}", configuration.GetValue<string>("Authentication:Schemas:Oauth2:CallbackPath"));
            }

            // Add OpenIdConnect authorization scheme if enabled
            if (configuration.GetValue<bool>("Authentication:Schemas:OpenIdConnect:Enabled"))
            {
                logger.Debug("Registering OpenIdConnect authentication scheme: {scheme}", LinkAdminConstants.AuthenticationSchemes.OpenIdConnect);
                if (!LinkAdminConstants.AuthenticationSchemes.OpenIdConnect.Equals(defaultChallengeScheme))
                    authSchemas.Add(LinkAdminConstants.AuthenticationSchemes.OpenIdConnect);

                authBuilder.AddOpenIdConnectAuthentication(options =>
                {
                    options.Environment = securityServiceOptions.Environment;
                    options.Authority = configuration.GetValue<string>("Authentication:Schemas:OpenIdConnect:Authority")!;
                    options.ClientId = configuration.GetValue<string>("Authentication:Schemas:OpenIdConnect:ClientId")!;
                    options.ClientSecret = configuration.GetValue<string>("Authentication:Schemas:OpenIdConnect:ClientSecret")!;
                    options.CallbackPath = configuration.GetValue<string>("Authentication:Schemas:OpenIdConnect:CallbackPath")!;
                    options.NameClaimType = configuration.GetValue<string>("Authentication:Schemas:OpenIdConnect:NameClaimType");
                    options.RoleClaimType = configuration.GetValue<string>("Authentication:Schemas:OpenIdConnect:RoleClaimType");
                });

                logger.Debug("Set OpenIdConnect Authentication Scheme: {Scheme} with the following settings: ", LinkAdminConstants.AuthenticationSchemes.OpenIdConnect);
                logger.Debug("Authority: {Authority}", configuration.GetValue<string>("Authentication:Schemas:OpenIdConnect:Authority"));
                logger.Debug("Client Id: {ClientId}", configuration.GetValue<string>("Authentication:Schemas:OpenIdConnect:ClientId"));
            }

            // Add JWT authorization scheme if enabled
            if (configuration.GetValue<bool>("Authentication:Schemas:Jwt:Enabled"))
            {
                logger.Debug("Registering JWT authentication scheme: {scheme}", LinkAdminConstants.AuthenticationSchemes.JwtBearerToken);
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

                logger.Debug("Set JWT Authentication Scheme: {Scheme} with the following settings: ", LinkAdminConstants.AuthenticationSchemes.JwtBearerToken);
                logger.Debug("Authority: {Authority}", configuration.GetValue<string>("Authentication:Schemas:Jwt:Authority"));
                logger.Debug("Audience: {Audience}", configuration.GetValue<string>("Authentication:Schemas:Jwt:Audience"));
            }

            // Add Link Bearer Token authorization schema
            if (!LinkAuthorizationConstants.AuthenticationSchemas.LinkBearerToken.Equals(defaultChallengeScheme))
                authSchemas.Add(LinkAuthorizationConstants.AuthenticationSchemas.LinkBearerToken);

            services.AddLinkBearerServiceAuthentication(logger, options =>
            {
                options.Environment = securityServiceOptions.Environment;
                options.Authority = configuration.GetValue<string>("LinkTokenService:Authority") ?? LinkAuthorizationConstants.LinkBearerService.LinkBearerIssuer;
                options.Audience = LinkAuthorizationConstants.LinkBearerService.LinkBearerAudience;   
                
                var linkTokenSigningKey = configuration.GetValue<string>("LinkTokenService:SigningKey");
                if (!string.IsNullOrEmpty(linkTokenSigningKey))
                {
                    options.SigningKey = linkTokenSigningKey;
                }
            });

            // Add Authorization
            services.AddAuthorization(builder =>
            {
                builder.AddPolicy(LinkAuthorizationConstants.LinkBearerService.AuthenticatedUserPolicyName, pb => {
                    pb.RequireAuthenticatedUser()
                        .AddAuthenticationSchemes([.. authSchemas]);
                });

                builder.AddPolicy(PolicyNames.IsLinkAdmin, AuthorizationPolicies.IsLinkAdmin());
            });

            return services;
        
        }

        public class SecurityServiceOptions
        {
            public IWebHostEnvironment Environment { get; set; } = null!;        
        }
    }
}