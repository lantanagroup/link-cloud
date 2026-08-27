using LantanaGroup.Link.Nhsn.App.Bff.Application.Interfaces.Infrastructure;
using LantanaGroup.Link.Nhsn.App.Bff.Settings;
using LantanaGroup.Link.Sdk.DependencyInjection;
using LantanaGroup.Link.Shared.Application.Extensions.Security;
using LantanaGroup.Link.Shared.Application.Interfaces.Services.Security.Token;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.Services.Security.Token;
using LantanaGroup.Link.Shared.Settings;

namespace LantanaGroup.Link.Nhsn.App.Bff.Infrastructure.Link.DependencyInjection;

public static class LinkGatewayRegistration
{
    // Registers LinkSdk and the BFF's Link gateway adapters.
    //
    // Does not call AddLinkBearerServiceAuthentication — that would register a second JwtBearer
    // scheme this service doesn't want, since it authenticates callers with the NHSN gateway JWT.
    // LinkBearerServiceOptions is configured directly instead, because LinkSdk reads only
    // AllowAnonymous from it to decide whether to mint an outbound system token.
    public static IServiceCollection AddLinkGateways(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<ServiceRegistry>(configuration.GetSection(ServiceRegistry.ConfigSectionName));
        services.Configure<LinkTokenServiceSettings>(configuration.GetSection(ConfigurationConstants.AppSettings.LinkTokenService));
        services.Configure<LinkCapabilitiesSettings>(configuration.GetSection(LinkCapabilitiesSettings.SectionName));

        var allowAnonymous = configuration.GetValue<bool?>("Authentication:AllowAnonymous") ?? false;
        services.Configure<BackendAuthenticationServiceExtension.LinkBearerServiceOptions>(options =>
        {
            options.AllowAnonymous = allowAnonymous;
        });

        services.AddSingleton<ICreateSystemToken, CreateSystemToken>();
        services.AddLinkSdk();

        services.AddScoped<IFacilityGateway, FacilityGateway>();

        return services;
    }
}
