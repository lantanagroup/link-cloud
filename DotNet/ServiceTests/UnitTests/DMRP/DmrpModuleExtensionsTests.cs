using LantanaGroup.Link.DMRP.Business;
using LantanaGroup.Link.DMRP.Business.Managers;
using LantanaGroup.Link.DMRP.Business.Queries;
using LantanaGroup.Link.DMRP.Controllers;
using LantanaGroup.Link.DMRP.Data.Entities;
using LantanaGroup.Link.DMRP.DependencyInjection;
using LantanaGroup.Link.Shared.Application.Models.Tenant;
using LantanaGroup.Link.Shared.Domain.Repositories.Interfaces;
using LantanaGroup.Link.Tenant.Repository.Context;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.DMRP
{
    /// <summary>
    /// DMRP is hosted in-process by the Tenant service. These cover the toggle that keeps the module
    /// inert for non-NHSN deployments.
    /// </summary>
    [Trait("Category", "UnitTests")]
    public class DmrpModuleExtensionsTests
    {
        private static WebApplicationBuilder CreateBuilder(bool? enabled)
        {
            var builder = WebApplication.CreateBuilder();

            var settings = new Dictionary<string, string?>();

            if (enabled.HasValue)
            {
                settings["DMRP:Enabled"] = enabled.Value.ToString();
            }

            builder.Configuration.AddInMemoryCollection(settings);

            // Stands in for what the real host registers before it adds the module.
            builder.Services.AddScoped<IFacilityOperations, HostFacilityOperations>();

            return builder;
        }

        /// <summary>
        /// Stands in for the host's implementation. Only its type matters here: the tests check which
        /// implementation the container hands out, never what it does.
        /// </summary>
        private sealed class HostFacilityOperations : IFacilityOperations
        {
            public Task CreateAsync(FacilityModel facility, CancellationToken cancellationToken = default) =>
                Task.CompletedTask;

            public Task UpdateAsync(FacilityModel existingFacility, FacilityModel updatedFacility,
                CancellationToken cancellationToken = default) => Task.CompletedTask;

            public Task DeleteAsync(string facilityId, CancellationToken cancellationToken = default) =>
                Task.CompletedTask;

            public Task SoftDeleteAsync(string facilityId, CancellationToken cancellationToken = default) =>
                Task.CompletedTask;

            public Task RestoreAsync(FacilityModel facility, CancellationToken cancellationToken = default) =>
                Task.CompletedTask;
        }

        [Fact]
        public void AddDmrpModule_registers_the_module_when_enabled()
        {
            var builder = CreateBuilder(enabled: true);
            var mvcBuilder = builder.Services.AddControllers();

            var registered = builder.AddDmrpModule<TenantDbContext, HostFacilityOperations>(mvcBuilder);

            Assert.True(registered);
            Assert.Contains(builder.Services, d => d.ServiceType == typeof(IEntityRepository<MeasureMapping>));
            Assert.Contains(builder.Services, d => d.ServiceType == typeof(IEntityRepository<FacilityReportingPlan>));
            Assert.Contains(builder.Services, d => d.ServiceType == typeof(IMeasureMappingManager));
            Assert.Contains(builder.Services, d => d.ServiceType == typeof(IMeasureMappingQueries));
            Assert.Contains(builder.Services, d => d.ServiceType == typeof(IFacilityReportingPlanManager));
            Assert.Contains(builder.Services, d => d.ServiceType == typeof(IFacilityReportingPlanQueries));
        }

        [Fact]
        public void AddDmrpModule_persists_through_the_host_context()
        {
            var builder = CreateBuilder(enabled: true);
            var mvcBuilder = builder.Services.AddControllers();

            builder.AddDmrpModule<TenantDbContext, HostFacilityOperations>(mvcBuilder);

            // The module must not stand up a context of its own; it repositories over the host's.
            var repository = Assert.Single(builder.Services,
                d => d.ServiceType == typeof(IEntityRepository<MeasureMapping>));

            Assert.Equal(typeof(TenantDbContext), repository.ImplementationType!.GetGenericArguments()[1]);
            Assert.DoesNotContain(builder.Services, d => d.ServiceType == typeof(TenantDbContext));
        }

        [Fact]
        public void AddDmrpModule_exposes_the_module_controllers_when_enabled()
        {
            var builder = CreateBuilder(enabled: true);
            var mvcBuilder = builder.Services.AddControllers();

            builder.AddDmrpModule<TenantDbContext, HostFacilityOperations>(mvcBuilder);

            var dmrpAssembly = typeof(MeasureMapping).Assembly;
            Assert.Contains(mvcBuilder.PartManager.ApplicationParts, p => p.Name == dmrpAssembly.GetName().Name);

            // The host discovers controllers through the application part, so confirm the module's
            // controllers are actually reachable rather than just that the assembly was added.
            var controllers = new ControllerFeature();
            mvcBuilder.PartManager.PopulateFeature(controllers);

            Assert.Contains(controllers.Controllers, c => c.AsType() == typeof(MeasureMappingsController));
            Assert.Contains(controllers.Controllers, c => c.AsType() == typeof(FacilityReportingPlansController));
        }

        [Theory]
        [InlineData(typeof(MeasureMappingsController), "api/dmrp/measure-mappings")]
        [InlineData(typeof(FacilityReportingPlansController), "api/dmrp/reporting-plans")]
        public void Module_controllers_use_the_routes_named_in_the_proposal(Type controller, string expectedRoute)
        {
            var route = controller.GetCustomAttributes(typeof(RouteAttribute), inherit: false)
                .Cast<RouteAttribute>()
                .Single();

            Assert.Equal(expectedRoute, route.Template);
        }

        [Fact]
        public void AddDmrpModule_leaves_the_facility_lookup_to_the_host()
        {
            var builder = CreateBuilder(enabled: true);
            var hostLookup = Mock.Of<IFacilityExistence>();

            builder.Services.AddSingleton(hostLookup);

            builder.AddDmrpModule<TenantDbContext, HostFacilityOperations>(builder.Services.AddControllers());

            var registration = Assert.Single(builder.Services, d => d.ServiceType == typeof(IFacilityExistence));
            Assert.Same(hostLookup, registration.ImplementationInstance);
        }

        [Fact]
        public void AddDmrpModule_registers_no_facility_lookup_of_its_own()
        {
            var builder = CreateBuilder(enabled: true);

            builder.AddDmrpModule<TenantDbContext, HostFacilityOperations>(builder.Services.AddControllers());

            Assert.DoesNotContain(builder.Services, d => d.ServiceType == typeof(IFacilityExistence));
        }

        [Fact]
        public void AddDmrpModule_puts_its_facility_operations_in_front_of_the_hosts()
        {
            var builder = CreateBuilder(enabled: true);

            builder.AddDmrpModule<TenantDbContext, HostFacilityOperations>(builder.Services.AddControllers());

            using var provider = BuildProviderWithModuleDependencies(builder);
            using var scope = provider.CreateScope();

            var resolved = scope.ServiceProvider.GetRequiredService<IFacilityOperations>();

            Assert.IsType<DmrpFacilityOperations>(resolved);

            // The host's implementation has to remain resolvable, because the module delegates to it.
            Assert.NotNull(scope.ServiceProvider.GetRequiredService<HostFacilityOperations>());
        }

        [Fact]
        public void AddDmrpModule_leaves_the_hosts_facility_operations_alone_when_disabled()
        {
            var builder = CreateBuilder(enabled: false);

            builder.AddDmrpModule<TenantDbContext, HostFacilityOperations>(builder.Services.AddControllers());

            var registration = Assert.Single(builder.Services, d => d.ServiceType == typeof(IFacilityOperations));

            Assert.Equal(typeof(HostFacilityOperations), registration.ImplementationType);
        }

        /// <summary>
        /// The module's facility operations take dependencies it registers over the host's database
        /// context. The tests here only resolve them, so the context and the repositories they need are
        /// faked rather than stood up.
        /// </summary>
        private static ServiceProvider BuildProviderWithModuleDependencies(WebApplicationBuilder builder)
        {
            builder.Services.AddLogging();
            builder.Services.AddScoped(_ => Mock.Of<IEntityRepository<MeasureMapping>>());
            builder.Services.AddScoped(_ => Mock.Of<IEntityRepository<FacilityReportingPlan>>());
            builder.Services.AddScoped(_ => Mock.Of<IFacilityExistence>());

            return builder.Services.BuildServiceProvider();
        }

        [Theory]
        [InlineData(false)]
        [InlineData(null)]
        public void AddDmrpModule_registers_nothing_when_disabled_or_unset(bool? enabled)
        {
            var builder = CreateBuilder(enabled);
            var mvcBuilder = builder.Services.AddControllers();

            var registered = builder.AddDmrpModule<TenantDbContext, HostFacilityOperations>(mvcBuilder);

            Assert.False(registered);
            Assert.DoesNotContain(builder.Services, d => d.ServiceType == typeof(IEntityRepository<MeasureMapping>));
            Assert.DoesNotContain(builder.Services, d => d.ServiceType == typeof(IMeasureMappingManager));
            Assert.DoesNotContain(builder.Services, d => d.ServiceType == typeof(IFacilityReportingPlanManager));

            var dmrpAssembly = typeof(MeasureMapping).Assembly;
            Assert.DoesNotContain(mvcBuilder.PartManager.ApplicationParts, p => p.Name == dmrpAssembly.GetName().Name);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(null)]
        public void AddDmrpModule_removes_the_hosts_auto_discovered_part_when_disabled(bool? enabled)
        {
            var builder = CreateBuilder(enabled);
            var mvcBuilder = builder.Services.AddControllers();

            // The Tenant build emits [assembly: ApplicationPart("DMRP")] for the project reference, so
            // in the real host the module's assembly is an application part before AddDmrpModule runs.
            // Recreate that here: the module must strip the part, or its controllers would be routable
            // without their services and every DMRP request would 500 instead of 404.
            var dmrpAssembly = typeof(MeasureMapping).Assembly;
            mvcBuilder.AddApplicationPart(dmrpAssembly);

            var registered = builder.AddDmrpModule<TenantDbContext, HostFacilityOperations>(mvcBuilder);

            Assert.False(registered);
            Assert.DoesNotContain(mvcBuilder.PartManager.ApplicationParts, p => p.Name == dmrpAssembly.GetName().Name);

            var controllers = new ControllerFeature();
            mvcBuilder.PartManager.PopulateFeature(controllers);
            Assert.DoesNotContain(controllers.Controllers, c => c.Assembly == dmrpAssembly);
        }
    }
}
