using LantanaGroup.Link.Nhsn.App.Bff.Application.Interfaces.Infrastructure;
using LantanaGroup.Link.Nhsn.App.Bff.Infrastructure.Concurrency;
using LantanaGroup.Link.Nhsn.App.Bff.Infrastructure.Link.Capabilities;
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
    public static IServiceCollection AddLinkGateways(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<ServiceRegistry>(configuration.GetSection(ServiceRegistry.ConfigSectionName));
        services.Configure<LinkTokenServiceSettings>(configuration.GetSection(ConfigurationConstants.AppSettings.LinkTokenService));
        services.Configure<LinkCapabilitiesSettings>(configuration.GetSection(LinkCapabilitiesSettings.SectionName));
        services.Configure<FacilityWriteLockSettings>(configuration.GetSection(FacilityWriteLockSettings.SectionName));

        var allowAnonymous = configuration.GetValue<bool?>("Authentication:AllowAnonymous") ?? false;
        services.Configure<BackendAuthenticationServiceExtension.LinkBearerServiceOptions>(options =>
        {
            options.AllowAnonymous = allowAnonymous;
        });

        services.AddSingleton<ICreateSystemToken, CreateSystemToken>();
        services.AddLinkSdk();
        services.AddHttpClient();

        services.AddScoped<IFacilityGateway, FacilityGateway>();
        services.AddScoped<IFhirConfigurationGateway, FhirConfigurationGateway>();
        services.AddSingleton<IDataAcquisitionRawClient, DataAcquisitionRawClient>();
        services.AddScoped<ICensusConfigurationGateway, CensusConfigurationGateway>();
        services.AddScoped<IQueryDispatchGateway, QueryDispatchGateway>();
        services.AddScoped<IFacilityWriteLock, SqlFacilityWriteLock>();
        services.AddSingleton<ISftpFileGateway, SftpFileFixtureGateway>();
        services.AddSingleton<ISftpConfigurationGateway, SftpConfigurationFixtureGateway>();
        services.AddSingleton<IPatientListGateway, PatientListFixtureGateway>();

        return services;
    }
}
