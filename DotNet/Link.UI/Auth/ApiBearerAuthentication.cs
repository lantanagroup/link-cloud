using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace Link.UI.Auth;

/// <summary>
/// Inbound bearer auth for the automation HTTP API.
/// The scheme is named and is not the app default, so cookie sign-in for the shell is unchanged.
/// When Authentication:ApiBearer:Enabled is false, ApiBearerPolicy allows the caller through.
/// ShellAccessGate still returns 503 for these routes when anonymous access is also off.
/// </summary>
public static class ApiBearerAuthentication
{
    public const string PolicyName = "ApiBearerPolicy";
    public const string SchemeName = "ApiBearer";
    public const string ConfigSection = "Authentication:ApiBearer";

    public static bool Add(IServiceCollection services, IConfiguration configuration)
    {
        var enabled = configuration.GetValue<bool>($"{ConfigSection}:Enabled");
        var authority = configuration[$"{ConfigSection}:Authority"];
        var audience = configuration[$"{ConfigSection}:Audience"];

        if (enabled)
        {
            if (string.IsNullOrWhiteSpace(authority) || string.IsNullOrWhiteSpace(audience))
            {
                throw new InvalidOperationException(
                    $"{ConfigSection} is enabled but {ConfigSection}:Authority/{ConfigSection}:Audience are not configured.");
            }

            services
                .AddAuthentication()
                .AddJwtBearer(SchemeName, options =>
                {
                    var validAudiences = BuildValidAudiences(audience);
                    options.Authority = authority;
                    options.Audience = audience;
                    options.RequireHttpsMetadata = true;
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidateAudience = true,
                        ValidAudiences = validAudiences,
                    };
                });
        }

        services.AddAuthorization(options =>
        {
            options.DefaultPolicy = new AuthorizationPolicyBuilder()
                .RequireAssertion(_ => true)
                .Build();

            if (enabled)
            {
                options.AddPolicy(PolicyName, policy =>
                {
                    policy.AddAuthenticationSchemes(SchemeName);
                    policy.RequireAuthenticatedUser();
                });
            }
            else
            {
                options.AddPolicy(PolicyName, policy => policy.RequireAssertion(_ => true));
            }
        });

        return enabled;
    }

    public static IReadOnlyCollection<string> BuildValidAudiences(string configuredAudience)
    {
        var normalized = configuredAudience.Trim().TrimEnd('/');
        var audiences = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            normalized
        };

        const string ApiUriPrefix = "api://";
        if (normalized.StartsWith(ApiUriPrefix, StringComparison.OrdinalIgnoreCase))
        {
            var rawAudience = normalized[ApiUriPrefix.Length..].TrimEnd('/');
            if (!string.IsNullOrWhiteSpace(rawAudience))
                audiences.Add(rawAudience);
        }
        else
        {
            audiences.Add($"{ApiUriPrefix}{normalized}");
        }

        return audiences;
    }
}
