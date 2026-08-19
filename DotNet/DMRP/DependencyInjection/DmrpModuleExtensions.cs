using LantanaGroup.Link.DMRP.Business.Managers;
using LantanaGroup.Link.DMRP.Business.Queries;
using LantanaGroup.Link.DMRP.Config;
using LantanaGroup.Link.DMRP.Data.Entities;
using LantanaGroup.Link.Shared.Domain.Repositories.Implementations;
using LantanaGroup.Link.Shared.Domain.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

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
        /// <returns>True when the module was registered, otherwise false.</returns>
        public static bool AddDmrpModule<TDbContext>(this WebApplicationBuilder builder, IMvcBuilder mvcBuilder)
            where TDbContext : DbContext
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

            return true;
        }
    }
}
