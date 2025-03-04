using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace LantanaGroup.Link.LinkAdmin.BFF.Infrastructure.Authentication.CdcSams
{
    public class SamsHandler : OAuthHandler<SamsOptions>
    {
        [Obsolete("ISystemClock is obsolete, use TimeProvider on AuthenticationSchemeOptions instead.")]
        public SamsHandler(IOptionsMonitor<SamsOptions> options, ILoggerFactory logger, UrlEncoder encoder, ISystemClock clock) 
            : base(options, logger, encoder, clock) { }

        public SamsHandler(IOptionsMonitor<SamsOptions> options, ILoggerFactory logger, UrlEncoder encoder) 
            : base(options, logger, encoder) { }

        protected override async Task<AuthenticationTicket> CreateTicketAsync(ClaimsIdentity identity, AuthenticationProperties properties, OAuthTokenResponse tokens)
        { 
            var endpoint = QueryHelpers.AddQueryString(Options.UserInformationEndpoint, "access_token", tokens.AccessToken!);

            var resposne = await Backchannel.GetAsync(endpoint, Context.RequestAborted);
            if(!resposne.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"An error occurred when retrieving SAMS user information ({resposne.StatusCode}). Please check the logs for more information.");
            }

            using (var payload = JsonDocument.Parse(await resposne.Content.ReadAsStringAsync(Context.RequestAborted)))
            {
                var context = new OAuthCreatingTicketContext(new ClaimsPrincipal(identity), properties, Context, Scheme, Options, Backchannel, tokens, payload.RootElement);
                context.RunClaimActions();
                await Options.Events.CreatingTicket(context);

                //TODO: Get Application specific claims

                return new AuthenticationTicket(context.Principal!, context.Properties, Scheme.Name);
            } 
        
        }


        
    }
}
