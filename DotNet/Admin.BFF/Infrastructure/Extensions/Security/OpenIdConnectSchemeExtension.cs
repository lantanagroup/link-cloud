using LantanaGroup.Link.LinkAdmin.BFF.Application.Interfaces.Infrastructure;
using LantanaGroup.Link.LinkAdmin.BFF.Settings;
using Microsoft.AspNetCore.Authentication;

namespace LantanaGroup.Link.LinkAdmin.BFF.Infrastructure.Extensions.Security
{
    public static class OpenIdConnectSchemeExtension
    {
        public static AuthenticationBuilder AddOpenIdConnectAuthentication(this AuthenticationBuilder builder, Action<OpenIdConnectOptions>? options)
        {
            var openIdConnectOptions = new OpenIdConnectOptions();
            options?.Invoke(openIdConnectOptions);

            builder.AddOpenIdConnect(LinkAdminConstants.AuthenticationSchemes.OpenIdConnect, options =>
            {
                options.Authority = openIdConnectOptions.Authority;
                options.ClientId = openIdConnectOptions.ClientId;
                options.ClientSecret = openIdConnectOptions.ClientSecret;
                options.RequireHttpsMetadata = !openIdConnectOptions.Environment.IsDevelopment();
                options.Scope.Add("email"); // openId and profile scopes are included by default
                options.SaveTokens = false;
                options.ResponseType = "code";
                options.CallbackPath = openIdConnectOptions.CallbackPath;
                options.MapInboundClaims = false;

                options.TokenValidationParameters = new()
                {
                    NameClaimType = openIdConnectOptions.NameClaimType,
                    RoleClaimType = openIdConnectOptions.RoleClaimType
                };

                options.Events.OnTokenValidated = context => {
                    
                    //increment login counter
                    var metrics = context.HttpContext.RequestServices.GetRequiredService<ILinkAdminMetrics>();
                    metrics.IncrementUserLoginCounter([
                        new KeyValuePair<string, object?>("auth.scheme", LinkAdminConstants.AuthenticationSchemes.OpenIdConnect)
                    ]);
                    return Task.CompletedTask;
                };
            });

            return builder;

        }

        public class OpenIdConnectOptions
        {
            public IWebHostEnvironment Environment { get; set; } = null!;
            public string Authority { get; set; } = null!;
            public string ClientId { get; set; } = null!;
            public string ClientSecret { get; set; } = null!;
            public string? NameClaimType { get; set; } = null!;
            public string? RoleClaimType { get; set; } = null!;
            public string CallbackPath { get; set; } = "/api/signin-oidc";


        }
    }
}
