using LantanaGroup.Link.LinkAdmin.BFF.Application.Interfaces.Infrastructure;
using LantanaGroup.Link.LinkAdmin.BFF.Settings;
using Microsoft.AspNetCore.Authentication;
using System.Net.Http.Headers;
using System.Text.Json;

namespace LantanaGroup.Link.LinkAdmin.BFF.Infrastructure.Extensions.Security
{
    public static class OAuthSchemeExtension
    {
        public static AuthenticationBuilder AddOAuthAuthentication(this AuthenticationBuilder builder, Action<OAuthOptions>? options)
        {
            var oauthOptions = new OAuthOptions();
            options?.Invoke(oauthOptions);

            builder.AddOAuth(LinkAdminConstants.AuthenticationSchemes.Oauth2, options =>
            {
                options.AuthorizationEndpoint = oauthOptions.AuthorizationEndpoint;
                options.TokenEndpoint = oauthOptions.TokenEndpoint;
                options.UserInformationEndpoint = oauthOptions.UserInformationEndpoint;
                options.ClientId = oauthOptions.ClientId;
                options.ClientSecret = oauthOptions.ClientSecret;
                options.CallbackPath = oauthOptions.CallbackPath;
                options.SaveTokens = false;
                options.Scope.Add("email");
                options.Scope.Add("profile");
                options.Scope.Add("openid");

                options.ClaimActions.MapJsonKey("sub", "sub");
                options.ClaimActions.MapJsonKey("email", "email");
                options.ClaimActions.MapJsonKey("name", "name");
                options.ClaimActions.MapJsonKey("given_name", "given_name");
                options.ClaimActions.MapJsonKey("family_name", "family_name");

                options.Events.OnCreatingTicket = async context =>
                {
                    var request = new HttpRequestMessage(HttpMethod.Get, context.Options.UserInformationEndpoint);
                    request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", context.AccessToken);

                    var response = await context.Backchannel.SendAsync(request, context.HttpContext.RequestAborted);
                    response.EnsureSuccessStatusCode();                    

                    var user = await response.Content.ReadFromJsonAsync<JsonElement>();                     

                    context.RunClaimActions(user);

                    //increment login counter
                    var metrics = context.HttpContext.RequestServices.GetRequiredService<ILinkAdminMetrics>();
                    metrics.IncrementUserLoginCounter([
                        new KeyValuePair<string, object?>("auth.scheme", LinkAdminConstants.AuthenticationSchemes.Oauth2)
                    ]);
                };

            });

            return builder;
        }

        public class OAuthOptions
        {
            public IWebHostEnvironment Environment { get; set; } = null!;
            public string ClientId { get; set; } = null!;
            public string ClientSecret { get; set; } = null!;
            public string AuthorizationEndpoint { get; set; } = null!;
            public string TokenEndpoint { get; set; } = null!;
            public string UserInformationEndpoint { get; set; } = null!;
            public string? CallbackPath { get; set; } = "/signin-oauth2";

        }
    }
}
