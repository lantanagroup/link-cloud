using LantanaGroup.Link.DMRP.Business;
using LantanaGroup.Link.DMRP.Business.Managers;
using LantanaGroup.Link.DMRP.Business.Queries;
using LantanaGroup.Link.DMRP.Controllers;
using LantanaGroup.Link.DMRP.Data.Entities;
using LantanaGroup.Link.DMRP.DependencyInjection;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.Services;
using LantanaGroup.Link.Shared.Domain.Repositories.Interfaces;
using LantanaGroup.Link.Tenant.Repository.Context;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;

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

            return builder;
        }

        [Fact]
        public void AddDmrpModule_registers_the_module_when_enabled()
        {
            var builder = CreateBuilder(enabled: true);
            var mvcBuilder = builder.Services.AddControllers();

            var registered = builder.AddDmrpModule<TenantDbContext>(mvcBuilder);

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

            builder.AddDmrpModule<TenantDbContext>(mvcBuilder);

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

            builder.AddDmrpModule<TenantDbContext>(mvcBuilder);

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
        public void AddDmrpModule_registers_the_facility_lookup_reporting_plans_validate_against()
        {
            var builder = CreateBuilder(enabled: true);

            builder.AddDmrpModule<TenantDbContext>(builder.Services.AddControllers());

            var registration = Assert.Single(builder.Services, d => d.ServiceType == typeof(IFacilityExistence));
            Assert.Equal(typeof(TenantApiFacilityExistence), registration.ImplementationType);
        }

        [Fact]
        public void AddDmrpModule_keeps_a_facility_lookup_the_host_already_registered()
        {
            var builder = CreateBuilder(enabled: true);
            var hostLookup = Mock.Of<IFacilityExistence>();

            builder.Services.AddSingleton(hostLookup);

            builder.AddDmrpModule<TenantDbContext>(builder.Services.AddControllers());

            var registration = Assert.Single(builder.Services, d => d.ServiceType == typeof(IFacilityExistence));
            Assert.Same(hostLookup, registration.ImplementationInstance);
        }

        [Fact]
        public void AddDmrpModule_leaves_an_absent_tenant_configuration_absent()
        {
            var builder = CreateBuilder(enabled: true);

            builder.AddDmrpModule<TenantDbContext>(builder.Services.AddControllers());

            var registry = builder.Services.BuildServiceProvider()
                .GetRequiredService<IOptions<ServiceRegistry>>().Value;

            Assert.Null(registry.TenantService);
        }

        [Fact]
        public void AddDmrpModule_defaults_the_relative_endpoint_when_the_host_configures_a_tenant_client()
        {
            var builder = CreateBuilder(enabled: true);
            builder.Services.Configure<ServiceRegistry>(registry =>
                registry.TenantService = new TenantServiceRegistration { CheckIfTenantExists = true });

            builder.AddDmrpModule<TenantDbContext>(builder.Services.AddControllers());

            var registry = builder.Services.BuildServiceProvider()
                .GetRequiredService<IOptions<ServiceRegistry>>().Value;

            Assert.Equal("facility/", registry.TenantService!.GetTenantRelativeEndpoint);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(null)]
        public void AddDmrpModule_registers_nothing_when_disabled_or_unset(bool? enabled)
        {
            var builder = CreateBuilder(enabled);
            var mvcBuilder = builder.Services.AddControllers();

            var registered = builder.AddDmrpModule<TenantDbContext>(mvcBuilder);

            Assert.False(registered);
            Assert.DoesNotContain(builder.Services, d => d.ServiceType == typeof(IEntityRepository<MeasureMapping>));
            Assert.DoesNotContain(builder.Services, d => d.ServiceType == typeof(IMeasureMappingManager));
            Assert.DoesNotContain(builder.Services, d => d.ServiceType == typeof(IFacilityReportingPlanManager));

            var dmrpAssembly = typeof(MeasureMapping).Assembly;
            Assert.DoesNotContain(mvcBuilder.PartManager.ApplicationParts, p => p.Name == dmrpAssembly.GetName().Name);
        }
    }
}
