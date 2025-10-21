using LantanaGroup.Link.LinkAdmin.BFF.Settings;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace LantanaGroup.Link.LinkAdmin.BFF.Infrastructure.Extensions;

public static class SwaggerOAuthExtension
{
    /// <summary>
    /// Conditionally adds OAuth2 security definitions and requirements to Swagger
    /// when OAuth is enabled and both endpoint URLs are configured.
    /// </summary>
    public static void AddOAuthSecurityIfConfigured(
        this SwaggerGenOptions options,
        bool oauthEnabled,
        string? authorizationEndpoint,
        string? tokenEndpoint)
    {
        if (!oauthEnabled
            || string.IsNullOrWhiteSpace(authorizationEndpoint)
            || string.IsNullOrWhiteSpace(tokenEndpoint))
        {
            return;
        }

        options.AddSecurityDefinition("OAuth", new OpenApiSecurityScheme
        {
            Description = "Authorization using OAuth",
            Name = "OAuth",
            Type = SecuritySchemeType.OAuth2,
            Scheme = LinkAdminConstants.AuthenticationSchemes.Oauth2,
            Flows = new OpenApiOAuthFlows
            {
                AuthorizationCode = new OpenApiOAuthFlow
                {
                    AuthorizationUrl = new Uri(authorizationEndpoint),
                    TokenUrl = new Uri(tokenEndpoint),
                    Scopes = new Dictionary<string, string>
                    {
                        { "openid", "OpenId" },
                        { "profile", "Profile" },
                        { "email", "Email" }
                    }
                }
            }
        });

        options.AddSecurityRequirement(new OpenApiSecurityRequirement
        {
            {
                new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference
                    {
                        Id = "OAuth",
                        Type = ReferenceType.SecurityScheme
                    },
                    Scheme = LinkAdminConstants.AuthenticationSchemes.Oauth2,
                    Name = "Oauth",
                    In = ParameterLocation.Header
                },
                new List<string>()
            }
        });
    }
}
