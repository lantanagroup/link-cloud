using LantanaGroup.Link.DMRP.Api;
using LantanaGroup.Link.DMRP.Business;
using LantanaGroup.Link.DMRP.Business.Managers;
using LantanaGroup.Link.DMRP.Business.Queries;
using LantanaGroup.Link.DMRP.Config;
using LantanaGroup.Link.DMRP.Data.Entities;
using LantanaGroup.Link.Shared.Domain.Repositories.Implementations;
using LantanaGroup.Link.Shared.Domain.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace LantanaGroup.Link.DMRP.DependencyInjection
{
    /// <summary>
    /// Registers the DMRP module into a host service (the Tenant service). DMRP is not deployed on its
    /// own; it adds controllers and reporting behavior to its host when enabled.
    /// </summary>
    public static class DmrpModuleExtensions
    {
        /// <summary>
        /// Adds the DMRP module to the host when <c>DMRP:Enabled</c> is set. The module's controllers are
        /// discovered through <paramref name="mvcBuilder"/>, so pass the builder returned by the host's
        /// call to <c>AddControllers</c>.
        /// </summary>
        /// <typeparam name="TDbContext">
        /// The host's database context. It must expose the DMRP entities, which lets the module persist
        /// through the host's context instead of opening a connection of its own.
        /// </typeparam>
        /// <typeparam name="THostFacilityOperations">
        /// The host's own facility operations. The module puts its behavior in front of these rather
        /// than replacing them, so the host stays the single place facilities are validated, persisted
        /// and scheduled.
        /// </typeparam>
        /// <returns>True when the module was registered, otherwise false.</returns>
        public static bool AddDmrpModule<TDbContext, THostFacilityOperations>(this WebApplicationBuilder builder,
            IMvcBuilder mvcBuilder)
            where TDbContext : DbContext
            where THostFacilityOperations : class, IFacilityOperations
        {
            ArgumentNullException.ThrowIfNull(builder);
            ArgumentNullException.ThrowIfNull(mvcBuilder);

            var section = builder.Configuration.GetSection(DmrpSettings.ConfigSectionName);
            builder.Services.Configure<DmrpSettings>(section);

            if (section.Get<DmrpSettings>()?.Enabled != true)
            {
                // The host's build emits [assembly: ApplicationPart("DMRP")] for the project reference,
                // so MVC discovers this module's controllers before AddDmrpModule runs. Left in place
                // without their services, those controllers turn every DMRP request into a 500; strip
                // the part so a disabled module has no routes at all.
                var moduleAssemblyName = typeof(DmrpModuleExtensions).Assembly.GetName().Name;
                foreach (var part in mvcBuilder.PartManager.ApplicationParts.Where(p => p.Name == moduleAssemblyName).ToList())
                {
                    mvcBuilder.PartManager.ApplicationParts.Remove(part);
                }

                return false;
            }

            // Discover the controllers in this assembly. Without this the host has no reason to load
            // the module's assembly and its routes would never be mapped.
            mvcBuilder.AddApplicationPart(typeof(DmrpModuleExtensions).Assembly);

            builder.Services.AddScoped<IEntityRepository<MeasureMapping>, EntityRepository<MeasureMapping, TDbContext>>();
            builder.Services.AddScoped<IEntityRepository<FacilityReportingPlan>, EntityRepository<FacilityReportingPlan, TDbContext>>();

            builder.Services.AddScoped<IMeasureMappingManager, MeasureMappingManager>();
            builder.Services.AddScoped<IMeasureMappingQueries, MeasureMappingQueries>();
            builder.Services.AddScoped<IFacilityReportingPlanManager, FacilityReportingPlanManager>();
            builder.Services.AddScoped<IFacilityReportingPlanQueries, FacilityReportingPlanQueries>();

            // Reporting plans come from what the module has already recorded. When the DMRP API client
            // lands, an implementation that refreshes those rows from the API replaces this one and
            // nothing downstream changes.
            builder.Services.AddScoped<IReportingPlanSource, DbBackedReportingPlanSource>();

            // Registered whether or not DMRP:Api is filled in. An environment with no API to talk to
            // simply never calls it, and the client says so plainly if something does; refusing to
            // register would instead surface as a resolve failure somewhere unrelated.
            builder.Services.AddHttpClient(DmrpApiClient.HttpClientName, client =>
            {
                // Bounded rather than left at HttpClient's 100-second default, which is far longer
                // than an admin GET should hang on a third party. The setting resolves itself, so a
                // value HttpClient would refuse cannot reach the setter and fail the first refresh.
                client.Timeout = (builder.Configuration
                    .GetSection(DmrpSettings.ConfigSectionName)
                    .Get<DmrpSettings>()?.Api ?? new DmrpApiSettings()).ResolvedTimeout;
            });
            builder.Services.AddSingleton<IDmrpApiTokenProvider, DmrpApiTokenProvider>();
            builder.Services.AddScoped<IDmrpApiClient, DmrpApiClient>();
            builder.Services.AddScoped<IDmrpReportingPlanSync, DmrpReportingPlanSync>();

            // One derivation of "what does this enrollment schedule", shared by the facility's stored
            // schedule and by the facility-facing look-ahead.
            builder.Services.AddScoped<IReportingPlanScheduleProjector, ReportingPlanScheduleProjector>();
            builder.Services.AddScoped<IFacilityReportingPlanLookAhead, FacilityReportingPlanLookAhead>();

            builder.Services.TryAddSingleton(TimeProvider.System);

            // The host's endpoints resolve IFacilityOperations, so taking over that registration is what
            // puts the module's behavior in front of the host's without moving a route. The host's own
            // implementation stays resolvable by its own type, which is what the module delegates to.
            //
            // Checked rather than assumed: RemoveAll on a service nobody registered removes nothing and
            // reports nothing, so a host that called this before registering its own implementation
            // would have that registration appended afterwards and win the resolve. The module would be
            // enabled with none of its facility behavior and no sign of it. Fail loudly instead.
            //
            // Either registration counts. The host normally registers the interface and lets the
            // TryAddScoped below make its implementation resolvable by type; a host that registers the
            // implementation type itself - to hand it dependencies of its own - has satisfied the same
            // requirement.
            var hostRegisteredItsOperations = builder.Services.Any(d =>
                d.ServiceType == typeof(IFacilityOperations) || d.ServiceType == typeof(THostFacilityOperations));

            if (!hostRegisteredItsOperations)
            {
                throw new InvalidOperationException(
                    $"The DMRP module decorates the host's {nameof(IFacilityOperations)}, but neither it nor " +
                    $"{typeof(THostFacilityOperations).Name} is registered. Register the host's implementation " +
                    $"before calling {nameof(AddDmrpModule)}.");
            }

            builder.Services.RemoveAll<IFacilityOperations>();
            builder.Services.TryAddScoped<THostFacilityOperations>();
            builder.Services.AddScoped<IFacilityOperations>(sp =>
                ActivatorUtilities.CreateInstance<DmrpFacilityOperations>(sp,
                    sp.GetRequiredService<THostFacilityOperations>()));

            return true;
        }
    }
}
