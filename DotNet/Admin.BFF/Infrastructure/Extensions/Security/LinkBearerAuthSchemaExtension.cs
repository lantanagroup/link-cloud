using LantanaGroup.Link.LinkAdmin.BFF.Settings;
using Microsoft.AspNetCore.Authentication;
using System.IdentityModel.Tokens.Jwt;

namespace LantanaGroup.Link.LinkAdmin.BFF.Infrastructure.Extensions.Security
{
    public static class LinkBearerAuthSchemaExtension
    {
        public static AuthenticationBuilder AddLinkBearerAuthentication(this AuthenticationBuilder builder, Action<LinkBearerOptions>? options)
        {
            var linkBearerOptions = new LinkBearerOptions();
            options?.Invoke(linkBearerOptions);

            JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();
            builder.AddJwtBearer(LinkAdminConstants.AuthenticationSchemes.LinkBearerToken, options =>
            {
                options.Authority = linkBearerOptions.Authority;
                options.Audience = linkBearerOptions.Audience;
                options.RequireHttpsMetadata = !linkBearerOptions.Environment.IsDevelopment();

                options.TokenValidationParameters = new()
                {
                    NameClaimType = linkBearerOptions.NameClaimType,
                    RoleClaimType = linkBearerOptions.RoleClaimType,
                    //avoid jwt confustion attacks (ie: circumvent token signature checking)
                    ValidTypes = linkBearerOptions.ValidTypes
                };
            });

            return builder;

        }

        public class LinkBearerOptions
        {
            public IWebHostEnvironment Environment { get; set; } = null!;
            public string? Authority { get; set; } = null!;
            public string? Audience { get; set; } = null!;
            public string? NameClaimType { get; set; } = null!;
            public string? RoleClaimType { get; set; } = null!;
            public string[]? ValidTypes { get; set; } = ["at+jwt", "JWT"];
        }

    }
}
