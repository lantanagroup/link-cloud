using LantanaGroup.Link.DMRP.Business.Managers;
using LantanaGroup.Link.DMRP.Business.Queries;
using LantanaGroup.Link.DMRP.Config;
using LantanaGroup.Link.DMRP.Data.Entities;
using LantanaGroup.Link.Shared.Application.Extensions;
using LantanaGroup.Link.Shared.Domain.Repositories.Implementations;
using LantanaGroup.Link.Shared.Domain.Repositories.Interceptors;
using LantanaGroup.Link.Shared.Domain.Repositories.Interfaces;
using LantanaGroup.Link.Tenant.Repository.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Reflection;

namespace IntegrationTests.DMRP
{
    /// <summary>
    /// DMRP persists through the Tenant service's context, so the fixture stands up a
    /// <see cref="TenantDbContext"/> and resolves the module's services against it.
    /// </summary>
    public class DmrpIntegrationTestFixture : IDisposable
    {
        public IServiceProvider ServiceProvider { get; private set; }
        private readonly IHost _host;
        private readonly string _dbPath;

        public DmrpIntegrationTestFixture()
        {
            var builder = Host.CreateApplicationBuilder();

            var assemblyVersion = Assembly.GetExecutingAssembly()
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? string.Empty;

            builder.SetupServiceInformation("DMRP", assemblyVersion);

            builder.Services.AddSingleton<UpdateBaseEntityInterceptor>();

            string dbName = $"dmrp_testdb_{Guid.NewGuid()}.db";
            _dbPath = Path.Combine(Path.GetTempPath(), dbName);
            var sqliteConnectionString = $"Data Source={_dbPath};";

            builder.Services.AddDbContext<TenantDbContext>((sp, options) =>
            {
                var updateBaseEntityInterceptor = sp.GetRequiredService<UpdateBaseEntityInterceptor>();
                options.UseSqlite(sqliteConnectionString);
                options.AddInterceptors(updateBaseEntityInterceptor);
            });

            builder.Services.AddScoped<IEntityRepository<MeasureMapping>, EntityRepository<MeasureMapping, TenantDbContext>>();
            builder.Services.AddScoped<IEntityRepository<FacilityReportingPlan>, EntityRepository<FacilityReportingPlan, TenantDbContext>>();

            builder.Services.AddScoped<IMeasureMappingManager, MeasureMappingManager>();
            builder.Services.AddScoped<IMeasureMappingQueries, MeasureMappingQueries>();
            builder.Services.AddScoped<IFacilityReportingPlanManager, FacilityReportingPlanManager>();
            builder.Services.AddScoped<IFacilityReportingPlanQueries, FacilityReportingPlanQueries>();

            builder.Services.AddLogging();

            _host = builder.Build();

            _host.StartAsync().GetAwaiter().GetResult();
            ServiceProvider = _host.Services;

            using var scope = ServiceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<TenantDbContext>();
            dbContext.Database.EnsureCreated();
        }

        public void Dispose()
        {
            using (var disposeScope = ServiceProvider.CreateScope())
            {
                var ctx = disposeScope.ServiceProvider.GetRequiredService<TenantDbContext>();
                ctx.Database.EnsureDeleted();
            }

            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            try
            {
                _host.StopAsync(cts.Token).GetAwaiter().GetResult();
            }
            catch (OperationCanceledException) { /* ignore - already stopped */ }

            _host.Dispose();

            if (File.Exists(_dbPath))
            {
                try { File.Delete(_dbPath); } catch (IOException) { /* best effort cleanup */ }
            }
        }
    }
}
