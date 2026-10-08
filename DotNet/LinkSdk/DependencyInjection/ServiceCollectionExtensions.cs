using LantanaGroup.Link.Sdk.Clients;
using LantanaGroup.Link.Shared.Application.Extensions.Security;
using LantanaGroup.Link.Shared.Application.Interfaces.Services.Security.Token;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

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
        services.AddSingleton<IDmrpServiceClient, DmrpServiceClient>();
        services.AddSingleton<INormalizationServiceClient, NormalizationServiceClient>();
        services.AddSingleton<IQueryDispatchServiceClient, QueryDispatchServiceClient>();
        services.AddSingleton<IReportServiceClient, ReportServiceClient>();
        services.AddSingleton<IMeasureEvalServiceClient, MeasureEvalServiceClient>();
        services.AddSingleton<IValidationServiceClient, ValidationServiceClient>();
        services.AddSingleton<ISubmissionServiceClient, SubmissionServiceClient>();
        services.AddSingleton<ITerminologyServiceClient, TerminologyServiceClient>();

        return services;
    }

    /// <summary>
    /// Registers <see cref="IMockDmrpServiceClient"/> unless one is already registered.
    /// </summary>
    /// <remarks>
    /// Not part of <see cref="AddLinkSdk"/>: the mock's address is not in <c>ServiceRegistry</c> for
    /// every caller, so each one says where it keeps it. <paramref name="baseUrl"/> runs when the client
    /// is first resolved, so a caller that never uses the mock never needs the address configured.
    /// </remarks>
    /// <param name="baseUrl">
    /// Returns the mock's root address. Throw from it, naming the setting, when the address is missing.
    /// </param>
    public static IServiceCollection AddMockDmrpServiceClient(this IServiceCollection services,
                                                              Func<IServiceProvider, string> baseUrl)
    {
        ArgumentNullException.ThrowIfNull(baseUrl);

        services.TryAddSingleton<IMockDmrpServiceClient>(sp => new MockDmrpServiceClient(
            baseUrl(sp),
            sp.GetRequiredService<IOptions<BackendAuthenticationServiceExtension.LinkBearerServiceOptions>>(),
            sp.GetRequiredService<IOptions<LinkTokenServiceSettings>>(),
            sp.GetRequiredService<ICreateSystemToken>()));

        return services;
    }
}
