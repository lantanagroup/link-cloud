using LantanaGroup.Link.Sdk.Clients;
using Microsoft.Extensions.DependencyInjection;

namespace LantanaGroup.Link.Sdk.DependencyInjection;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers all Link SDK service clients.
    /// Requires <c>IOptions&lt;ServiceRegistry&gt;</c>, <c>IOptions&lt;LinkBearerServiceOptions&gt;</c>,
    /// <c>IOptions&lt;LinkTokenServiceSettings&gt;</c>, and <c>ICreateSystemToken</c> to be available in DI.
    /// Each client resolves its own base URL from <c>ServiceRegistry</c>.
    /// </summary>
    public static IServiceCollection AddLinkSdk(this IServiceCollection services)
    {
        services.AddSingleton<IAdminBffIntegrationClient, AdminBffIntegrationClient>();
        services.AddSingleton<IFacilityServiceClient, FacilityServiceClient>();
        services.AddSingleton<ICensusServiceClient, CensusServiceClient>();
        services.AddSingleton<IDataAcquisitionServiceClient, DataAcquisitionServiceClient>();
        services.AddSingleton<INormalizationServiceClient, NormalizationServiceClient>();
        services.AddSingleton<IQueryDispatchServiceClient, QueryDispatchServiceClient>();
        services.AddSingleton<IReportServiceClient, ReportServiceClient>();
        services.AddSingleton<IMeasureEvalServiceClient, MeasureEvalServiceClient>();
        services.AddSingleton<IValidationServiceClient, ValidationServiceClient>();
        services.AddSingleton<ISubmissionServiceClient, SubmissionServiceClient>();

        return services;
    }
}
