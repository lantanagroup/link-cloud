using LantanaGroup.Link.DMRP.Business;
using LantanaGroup.Link.DMRP.Business.Managers;
using LantanaGroup.Link.DMRP.Business.Queries;
using LantanaGroup.Link.DMRP.Controllers;
using LantanaGroup.Link.DMRP.Data.Entities;
using LantanaGroup.Link.DMRP.DependencyInjection;
using LantanaGroup.Link.DMRP.MockDmrp;
using LantanaGroup.Link.DMRP.Scheduling;
using LantanaGroup.Link.Sdk.Clients;
using LantanaGroup.Link.Shared.Application.Extensions.Quartz;
using LantanaGroup.Link.Shared.Application.Models.Tenant;
using LantanaGroup.Link.Shared.Domain.Repositories.Interfaces;
using LantanaGroup.Link.Tenant.Repository.Context;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using LantanaGroup.Link.Shared.Application.Extensions.Security;
using LantanaGroup.Link.Shared.Application.Interfaces.Services.Security.Token;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using Microsoft.Extensions.Options;
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
        /// <summary>
        /// Stands in for the host's classic job group. Deliberately not the real
        /// <c>ReportSchedulingJobs.ClassicJobGroup</c>: the module is supposed to carry whatever the
        /// host names, and a value only these tests use is what shows it does.
        /// </summary>
        private const string ClassicGroup = "HostClassicReportJobs";

        private static WebApplicationBuilder CreateBuilder(bool? enabled,
                                                           Dictionary<string, string?>? extraSettings = null)
        {
            var builder = WebApplication.CreateBuilder();

            var settings = extraSettings ?? new Dictionary<string, string?>();

            if (enabled.HasValue)
            {
                settings["DMRP:Enabled"] = enabled.Value.ToString();
            }

            builder.Configuration.AddInMemoryCollection(settings);

            // Stands in for what the real host registers before it adds the module.
            builder.Services.AddScoped<IFacilityOperations, HostFacilityOperations>();

            var facilityDirectory = Mock.Of<IFacilityDirectory>();
            builder.Services.AddSingleton(facilityDirectory);
            builder.Services.AddSingleton<IFacilityTimeZoneSource>(facilityDirectory);

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

            var registered = builder.AddDmrpModule<TenantDbContext, HostFacilityOperations>(mvcBuilder, ClassicGroup);

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

            builder.AddDmrpModule<TenantDbContext, HostFacilityOperations>(mvcBuilder, ClassicGroup);

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

            builder.AddDmrpModule<TenantDbContext, HostFacilityOperations>(mvcBuilder, ClassicGroup);

            var dmrpAssembly = typeof(MeasureMapping).Assembly;
            Assert.Contains(mvcBuilder.PartManager.ApplicationParts, p => p.Name == dmrpAssembly.GetName().Name);

            // The host discovers controllers through the application part, so confirm the module's
            // controllers are actually reachable rather than just that the assembly was added.
            var controllers = new ControllerFeature();
            mvcBuilder.PartManager.PopulateFeature(controllers);

            Assert.Contains(controllers.Controllers, c => c.AsType() == typeof(MeasureMappingsController));
            Assert.Contains(controllers.Controllers, c => c.AsType() == typeof(FacilityReportingPlansController));
            Assert.Contains(controllers.Controllers, c => c.AsType() == typeof(DmrpStatusController));
        }

        /// <summary>
        /// Tenant reads the mock's own switch. It turns the write-through on only with an address to write
        /// to, and anything but a boolean true is off, the same way the mock reads it.
        /// </summary>
        [Theory]
        [InlineData("true", "http://mock-dmrp-api:8080", true)]
        [InlineData("True", "http://mock-dmrp-api:8080", true)]
        [InlineData("false", "http://mock-dmrp-api:8080", false)]
        [InlineData(null, "http://mock-dmrp-api:8080", false)]
        [InlineData("yes", "http://mock-dmrp-api:8080", false)]
        [InlineData("true", null, false)]
        [InlineData("true", " ", false)]
        public void AddDmrpModule_reads_the_mock_switch(string? mockEnabled, string? baseUrl, bool expected)
        {
            var builder = CreateBuilder(enabled: true, MockSettings(mockEnabled, baseUrl));

            builder.AddDmrpModule<TenantDbContext, HostFacilityOperations>(builder.Services.AddControllers(),
                                                                           ClassicGroup);

            var status = Assert.Single(builder.Services, d => d.ServiceType == typeof(IMockDmrpStatus));
            Assert.Equal(expected, ((IMockDmrpStatus)status.ImplementationInstance!).IsEnabled);
        }

        [Fact]
        public void AddDmrpModule_registers_the_mock_client_only_when_the_mock_is_on()
        {
            var on = CreateBuilder(enabled: true, MockSettings("true", "http://mock-dmrp-api:8080"));
            var off = CreateBuilder(enabled: true, MockSettings("false", "http://mock-dmrp-api:8080"));

            on.AddDmrpModule<TenantDbContext, HostFacilityOperations>(on.Services.AddControllers(), ClassicGroup);
            off.AddDmrpModule<TenantDbContext, HostFacilityOperations>(off.Services.AddControllers(), ClassicGroup);

            Assert.Contains(on.Services, d => d.ServiceType == typeof(IMockDmrpServiceClient));
            Assert.DoesNotContain(off.Services, d => d.ServiceType == typeof(IMockDmrpServiceClient));
        }

        /// <summary>
        /// With DMRP off the status route still answers, and it says the mock is off whatever its switch says:
        /// the write-through lives inside the module.
        /// </summary>
        [Fact]
        public void AddDmrpModule_registers_a_switched_off_mock_status_when_disabled()
        {
            var builder = CreateBuilder(enabled: false, MockSettings("true", "http://mock-dmrp-api:8080"));

            builder.AddDmrpModule<TenantDbContext, HostFacilityOperations>(builder.Services.AddControllers(),
                                                                           ClassicGroup);

            var status = Assert.Single(builder.Services, d => d.ServiceType == typeof(IMockDmrpStatus));
            Assert.False(((IMockDmrpStatus)status.ImplementationInstance!).IsEnabled);
            Assert.DoesNotContain(builder.Services, d => d.ServiceType == typeof(IMockDmrpServiceClient));
        }

        private static Dictionary<string, string?> MockSettings(string? mockEnabled, string? baseUrl) => new()
        {
            [MockDmrpStatus.EnabledConfigurationKey] = mockEnabled,
            ["DMRP:Api:BaseUrl"] = baseUrl
        };

        [Theory]
        [InlineData(typeof(MeasureMappingsController), "api/dmrp/measure-mappings")]
        [InlineData(typeof(FacilityReportingPlansController), "api/dmrp/reporting-plans")]
        [InlineData(typeof(DmrpStatusController), "api/dmrp/dmrp-status")]
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

            builder.AddDmrpModule<TenantDbContext, HostFacilityOperations>(builder.Services.AddControllers(), ClassicGroup);

            var registration = Assert.Single(builder.Services, d => d.ServiceType == typeof(IFacilityExistence));
            Assert.Same(hostLookup, registration.ImplementationInstance);
        }

        [Fact]
        public void AddDmrpModule_registers_no_facility_lookup_of_its_own()
        {
            var builder = CreateBuilder(enabled: true);

            builder.AddDmrpModule<TenantDbContext, HostFacilityOperations>(builder.Services.AddControllers(), ClassicGroup);

            Assert.DoesNotContain(builder.Services, d => d.ServiceType == typeof(IFacilityExistence));
        }

        [Fact]
        public void AddDmrpModule_registers_the_reporting_period_resolver()
        {
            var builder = CreateBuilder(enabled: true);

            builder.AddDmrpModule<TenantDbContext, HostFacilityOperations>(builder.Services.AddControllers(), ClassicGroup);

            var registration = Assert.Single(builder.Services,
                d => d.ServiceType == typeof(IFacilityReportingPeriodResolver));
            Assert.Equal(typeof(FacilityReportingPeriodResolver), registration.ImplementationType);
        }

        /// <summary>
        /// Enabled, the module reconciles its zone jobs at boot rather than leaving a stale or
        /// missing job for the classic scheduler's shared Quartz scheduler to fire (or not) blind.
        /// </summary>
        [Fact]
        public void AddDmrpModule_registers_the_reconciler_and_boot_reconcile_hosted_service_when_enabled()
        {
            var builder = CreateBuilder(enabled: true);

            builder.AddDmrpModule<TenantDbContext, HostFacilityOperations>(builder.Services.AddControllers(), ClassicGroup);

            Assert.Contains(builder.Services, d => d.ServiceType == typeof(IDmrpNightlyJobReconciler));

            var hostedService = Assert.Single(builder.Services,
                d => d.ServiceType == typeof(IHostedService));
            Assert.Equal(typeof(DmrpNightlyScheduleHostedService), hostedService.ImplementationType);
        }

        /// <summary>
        /// The hosted service deletes the host's classic jobs before it starts the scheduler, so it
        /// needs the group they are in. The host names it; the module only carries it through.
        /// </summary>
        [Fact]
        public void AddDmrpModule_carries_the_hosts_classic_job_group_to_the_hosted_service()
        {
            var builder = CreateBuilder(enabled: true);

            builder.AddDmrpModule<TenantDbContext, HostFacilityOperations>(builder.Services.AddControllers(), ClassicGroup);

            var registration = Assert.Single(builder.Services,
                d => d.ServiceType == typeof(DmrpSchedulingHostOptions));
            var options = Assert.IsType<DmrpSchedulingHostOptions>(registration.ImplementationInstance);
            Assert.Equal(ClassicGroup, options.ClassicJobGroup);
        }

        /// <summary>
        /// A blank group would have the hosted service sweep Quartz's own default group at every boot.
        /// Refuse it where the host can see it rather than at the first start.
        /// </summary>
        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public void AddDmrpModule_throws_when_the_host_names_no_classic_job_group(string classicJobGroup)
        {
            var builder = CreateBuilder(enabled: true);
            var mvcBuilder = builder.Services.AddControllers();

            Assert.Throws<ArgumentException>(() =>
                builder.AddDmrpModule<TenantDbContext, HostFacilityOperations>(mvcBuilder, classicJobGroup));
        }

        /// <summary>
        /// Where a facility is lives in the host's records, which the module cannot see. Like the
        /// existence check, the host supplies it and the module adds no registration of its own.
        /// </summary>
        [Fact]
        public void AddDmrpModule_leaves_the_time_zone_source_to_the_host()
        {
            var builder = CreateBuilder(enabled: true);
            builder.Services.RemoveAll<IFacilityTimeZoneSource>();

            var hostSource = Mock.Of<IFacilityTimeZoneSource>();
            builder.Services.AddSingleton(hostSource);

            builder.AddDmrpModule<TenantDbContext, HostFacilityOperations>(builder.Services.AddControllers(), ClassicGroup);

            var registration = Assert.Single(builder.Services, d => d.ServiceType == typeof(IFacilityTimeZoneSource));
            Assert.Same(hostSource, registration.ImplementationInstance);
        }

        /// <summary>
        /// Without a timezone source the module would start, then fail every facility save and every
        /// look-ahead read that needs a period, as a resolve error far from the cause. Fail at startup
        /// instead, naming what is missing.
        /// </summary>
        [Fact]
        public void AddDmrpModule_throws_when_the_host_registers_no_time_zone_source()
        {
            var builder = CreateBuilder(enabled: true);
            builder.Services.RemoveAll<IFacilityTimeZoneSource>();

            var mvcBuilder = builder.Services.AddControllers();

            var exception = Assert.Throws<InvalidOperationException>(() =>
                builder.AddDmrpModule<TenantDbContext, HostFacilityOperations>(mvcBuilder, ClassicGroup));

            Assert.Contains(nameof(IFacilityTimeZoneSource), exception.Message);
        }

        /// <summary>
        /// Without a facility directory the nightly job would have no way to enumerate facilities.
        /// Fail at startup instead of leaving the job to fail silently every time it fires.
        /// </summary>
        [Fact]
        public void AddDmrpModule_throws_when_the_host_registers_no_facility_directory()
        {
            var builder = CreateBuilder(enabled: true);
            builder.Services.RemoveAll<IFacilityDirectory>();

            var mvcBuilder = builder.Services.AddControllers();

            var exception = Assert.Throws<InvalidOperationException>(() =>
                builder.AddDmrpModule<TenantDbContext, HostFacilityOperations>(mvcBuilder, ClassicGroup));

            Assert.Contains(nameof(IFacilityDirectory), exception.Message);
        }

        /// <summary>
        /// A module that is off needs nothing from the host, so a missing timezone source is not an error.
        /// </summary>
        [Fact]
        public void AddDmrpModule_does_not_require_a_time_zone_source_when_disabled()
        {
            var builder = CreateBuilder(enabled: false);
            builder.Services.RemoveAll<IFacilityTimeZoneSource>();

            var registered = builder.AddDmrpModule<TenantDbContext, HostFacilityOperations>(
                builder.Services.AddControllers(), ClassicGroup);

            Assert.False(registered);
        }

        /// <summary>
        /// Disabled, the module still registers the reconciler and a cleanup hosted service, so any
        /// jobs left over from a previous run with the flag on are swept before the classic
        /// scheduler starts its shared Quartz scheduler.
        /// </summary>
        [Fact]
        public void AddDmrpModule_registers_the_reconciler_and_boot_cleanup_hosted_service_when_disabled()
        {
            var builder = CreateBuilder(enabled: false);

            builder.AddDmrpModule<TenantDbContext, HostFacilityOperations>(builder.Services.AddControllers(), ClassicGroup);

            Assert.Contains(builder.Services, d => d.ServiceType == typeof(IDmrpNightlyJobReconciler));

            var hostedService = Assert.Single(builder.Services,
                d => d.ServiceType == typeof(IHostedService));
            Assert.Equal(typeof(DmrpNightlyScheduleCleanupService), hostedService.ImplementationType);
        }

        /// <summary>
        /// The module decorates whatever the host registered, so it has to be registered first.
        /// Getting that order wrong is otherwise invisible: RemoveAll finds nothing to remove, the
        /// host's later registration wins the resolve, and DMRP runs with none of its facility
        /// behavior.
        /// </summary>
        [Fact]
        public void AddDmrpModule_refuses_to_decorate_a_host_that_registered_nothing()
        {
            var builder = CreateBuilder(enabled: true);
            builder.Services.RemoveAll<IFacilityOperations>();

            var mvcBuilder = builder.Services.AddControllers();

            var exception = Assert.Throws<InvalidOperationException>(() =>
                builder.AddDmrpModule<TenantDbContext, HostFacilityOperations>(mvcBuilder, ClassicGroup));

            Assert.Contains(nameof(IFacilityOperations), exception.Message);
        }

        /// <summary>
        /// A host that registers its implementation by type rather than behind the interface has met
        /// the same requirement, and the module resolves it by type regardless.
        /// </summary>
        [Fact]
        public void AddDmrpModule_accepts_a_host_that_registered_only_the_implementation_type()
        {
            var builder = CreateBuilder(enabled: true);
            builder.Services.RemoveAll<IFacilityOperations>();
            builder.Services.AddScoped(_ => new HostFacilityOperations());

            var registered = builder.AddDmrpModule<TenantDbContext, HostFacilityOperations>(
                builder.Services.AddControllers(), ClassicGroup);

            Assert.True(registered);
        }

        /// <summary>
        /// A host that never registered it is only a problem when the module is on. Disabled, the
        /// module leaves the host exactly as it found it.
        /// </summary>
        [Fact]
        public void AddDmrpModule_does_not_check_the_hosts_registration_when_disabled()
        {
            var builder = CreateBuilder(enabled: false);
            builder.Services.RemoveAll<IFacilityOperations>();

            var registered = builder.AddDmrpModule<TenantDbContext, HostFacilityOperations>(
                builder.Services.AddControllers(), ClassicGroup);

            Assert.False(registered);
        }

        [Fact]
        public void AddDmrpModule_puts_its_facility_operations_in_front_of_the_hosts()
        {
            var builder = CreateBuilder(enabled: true);

            builder.AddDmrpModule<TenantDbContext, HostFacilityOperations>(builder.Services.AddControllers(), ClassicGroup);

            using var provider = BuildProviderWithModuleDependencies(builder);
            using var scope = provider.CreateScope();

            var resolved = scope.ServiceProvider.GetRequiredService<IFacilityOperations>();

            Assert.IsType<DmrpFacilityOperations>(resolved);

            // The host's implementation has to remain resolvable, because the module delegates to it.
            Assert.NotNull(scope.ServiceProvider.GetRequiredService<HostFacilityOperations>());
        }

        /// <summary>
        /// With the mock switched on, the write-through is one more layer outside the DMRP operations; with
        /// it off, nothing changes from today.
        /// </summary>
        [Theory]
        [InlineData("true", typeof(MockDmrpSyncFacilityOperations))]
        [InlineData("false", typeof(DmrpFacilityOperations))]
        public void AddDmrpModule_puts_the_mock_write_through_outside_the_dmrp_operations_only_when_the_mock_is_on(
            string mockEnabled, Type expected)
        {
            var builder = CreateBuilder(enabled: true, MockSettings(mockEnabled, "http://mock-dmrp-api:8080"));

            builder.AddDmrpModule<TenantDbContext, HostFacilityOperations>(builder.Services.AddControllers(),
                                                                           ClassicGroup);

            // The mock client authenticates with the host's Link token plumbing.
            builder.Services.AddSingleton(Options.Create(
                new BackendAuthenticationServiceExtension.LinkBearerServiceOptions { AllowAnonymous = true }));
            builder.Services.AddSingleton(Options.Create(new LinkTokenServiceSettings()));
            builder.Services.AddSingleton(Mock.Of<ICreateSystemToken>());

            using var provider = BuildProviderWithModuleDependencies(builder);
            using var scope = provider.CreateScope();

            Assert.IsType(expected, scope.ServiceProvider.GetRequiredService<IFacilityOperations>());
        }

        [Fact]
        public void AddDmrpModule_leaves_the_hosts_facility_operations_alone_when_disabled()
        {
            var builder = CreateBuilder(enabled: false);

            builder.AddDmrpModule<TenantDbContext, HostFacilityOperations>(builder.Services.AddControllers(), ClassicGroup);

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

            // DmrpFacilityOperations now also takes IDmrpNightlyJobReconciler, which needs Quartz's
            // ISchedulerFactory to construct. Nothing here calls CreateAsync/UpdateAsync/RestoreAsync,
            // so the in-memory registration only has to make the graph resolvable.
            builder.Services.RegisterQuartzDatabaseInTest();

            return builder.Services.BuildServiceProvider();
        }

        [Theory]
        [InlineData(false)]
        [InlineData(null)]
        public void AddDmrpModule_registers_nothing_when_disabled_or_unset(bool? enabled)
        {
            var builder = CreateBuilder(enabled);
            var mvcBuilder = builder.Services.AddControllers();

            var registered = builder.AddDmrpModule<TenantDbContext, HostFacilityOperations>(mvcBuilder, ClassicGroup);

            Assert.False(registered);
            Assert.DoesNotContain(builder.Services, d => d.ServiceType == typeof(IEntityRepository<MeasureMapping>));
            Assert.DoesNotContain(builder.Services, d => d.ServiceType == typeof(IMeasureMappingManager));
            Assert.DoesNotContain(builder.Services, d => d.ServiceType == typeof(IFacilityReportingPlanManager));

            AssertOnlyTheStatusControllerIsRoutable(mvcBuilder);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(null)]
        public void AddDmrpModule_serves_only_the_status_route_when_disabled(bool? enabled)
        {
            var builder = CreateBuilder(enabled);
            var mvcBuilder = builder.Services.AddControllers();

            // The Tenant build emits [assembly: ApplicationPart("DMRP")] for the project reference, so
            // in the real host the module's assembly is an application part before AddDmrpModule runs.
            // Recreate that here: the module must hide every controller but the status one, or they would
            // be routable without their services and every DMRP request would 500 instead of 404.
            var dmrpAssembly = typeof(MeasureMapping).Assembly;
            mvcBuilder.AddApplicationPart(dmrpAssembly);

            var registered = builder.AddDmrpModule<TenantDbContext, HostFacilityOperations>(mvcBuilder, ClassicGroup);

            Assert.False(registered);
            Assert.Single(mvcBuilder.PartManager.ApplicationParts, p => p.Name == dmrpAssembly.GetName().Name);
            AssertOnlyTheStatusControllerIsRoutable(mvcBuilder);
        }

        private static void AssertOnlyTheStatusControllerIsRoutable(IMvcBuilder mvcBuilder)
        {
            var dmrpAssembly = typeof(MeasureMapping).Assembly;
            var controllers = new ControllerFeature();
            mvcBuilder.PartManager.PopulateFeature(controllers);

            var routable = Assert.Single(controllers.Controllers, c => c.Assembly == dmrpAssembly);
            Assert.Equal(typeof(DmrpStatusController), routable.AsType());
        }
    }
}
