using LantanaGroup.Link.LinkAdmin.BFF.Settings;
using Microsoft.AspNetCore.Authentication;
using System.IdentityModel.Tokens.Jwt;

namespace LantanaGroup.Link.LinkAdmin.BFF.Infrastructure.Extensions.Security
{
    public static class JwtBearerAuthSchemeExtension
    {
        public static AuthenticationBuilder AddJwTBearerAuthentication(this AuthenticationBuilder builder, Action<JwTBearerOptions>? options)
        {
            var jwtBearerOptions = new JwTBearerOptions();
            options?.Invoke(jwtBearerOptions);

            JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();
            builder.AddJwtBearer(LinkAdminConstants.AuthenticationSchemes.JwtBearerToken, options =>
            {
                options.Authority = jwtBearerOptions.Authority;
                options.Audience = jwtBearerOptions.Audience;
                options.RequireHttpsMetadata = jwtBearerOptions.RequireHttpsMetadata;
                options.MapInboundClaims = false;

                options.TokenValidationParameters = new()
                {
                    NameClaimType = jwtBearerOptions.NameClaimType,
                    RoleClaimType = jwtBearerOptions.RoleClaimType,
                    //avoid jwt confustion attacks (ie: circumvent token signature checking)
                    ValidTypes = jwtBearerOptions.ValidTypes
                };
            });

            return builder;

        }
    }

    public class JwTBearerOptions
    {
        public IWebHostEnvironment Environment { get; set; } = null!;
        public string? Authority { get; set; } = null!;
        public string? Audience { get; set; } = null!;
        public bool RequireHttpsMetadata { get; set; } = true;
        public string? NameClaimType { get; set; } = null!;
        public string? RoleClaimType { get; set; } = null!;
        public string[]? ValidTypes { get; set; } = ["at+jwt", "JWT"];
    }
}
