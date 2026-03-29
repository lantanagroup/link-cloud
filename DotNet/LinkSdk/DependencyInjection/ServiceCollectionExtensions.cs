using LantanaGroup.Link.Sdk.ApiClient;
using LantanaGroup.Link.Sdk.Clients;
using Microsoft.Extensions.DependencyInjection;

namespace LantanaGroup.Link.Sdk.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddLinkSdk(this IServiceCollection services, ApiClientSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        services.AddSingleton(settings);

        services.AddTransient<FacilityServiceClient>();
        services.AddTransient<NormalizationServiceClient>();
        services.AddTransient<DataAcquisitionServiceClient>();
        services.AddTransient<ReportServiceClient>();
        services.AddTransient<ValidationServiceClient>();
        services.AddTransient<CensusServiceClient>();
        services.AddTransient<MeasureEvalServiceClient>();

        return services;
    }
}
