using LantanaGroup.Link.DMRP.Business;
using LantanaGroup.Link.DMRP.DependencyInjection;
using LantanaGroup.Link.Shared.Application.Extensions;
using LantanaGroup.Link.Shared.Domain.Repositories.Interceptors;
using LantanaGroup.Link.Tenant.Repository.Context;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using System.Reflection;

namespace IntegrationTests.DMRP
{
    /// <summary>
    /// DMRP persists through the Tenant service's context, so the fixture stands up a
    /// <see cref="TenantDbContext"/> and registers the module against it through
    /// <see cref="DmrpModuleExtensions.AddDmrpModule{TDbContext}"/> — the same entry point the Tenant
    /// service uses, so the two cannot drift.
    /// </summary>
    public class DmrpIntegrationTestFixture : IDisposable
    {
        public IServiceProvider ServiceProvider { get; private set; }

        public Mock<IFacilityExistence> FacilityExistenceMock { get; } = new();

        private readonly WebApplication _host;
        private readonly string _dbPath;

        public DmrpIntegrationTestFixture()
        {
            var builder = WebApplication.CreateBuilder();

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

            // The module only registers itself when the flag is set, so turn it on for the fixture.
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DMRP:Enabled"] = "true"
            });

            ResetFacilityExistence();

            builder.Services.AddSingleton<IFacilityExistence>(FacilityExistenceMock.Object);

            var registered = builder.AddDmrpModule<TenantDbContext>(builder.Services.AddControllers());
            if (!registered)
            {
                throw new InvalidOperationException("The DMRP module did not register; the fixture cannot resolve its services.");
            }

            builder.Services.AddLogging();

            // Built but never started: AddDmrpModule needs a WebApplicationBuilder, and starting the
            // result would bind Kestrel to a port for a fixture that only resolves services.
            _host = builder.Build();
            ServiceProvider = _host.Services;

            using var scope = ServiceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<TenantDbContext>();
            dbContext.Database.EnsureCreated();
        }

        /// <summary>
        /// Restores the default "every facility exists" stub and drops any setup a test added. The
        /// mock is fixture-scoped and shared by every test in the collection, so a test that stubs a
        /// missing facility would otherwise leave that answer in place for whatever runs next —
        /// xUnit gives no ordering guarantee, so the resulting failure would move around.
        /// <see cref="Mock{T}.Object"/> survives the reset, so the singleton already handed to the
        /// container stays valid.
        /// </summary>
        public void ResetFacilityExistence()
        {
            FacilityExistenceMock.Reset();

            FacilityExistenceMock
                .Setup(s => s.ExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);
        }

        public void Dispose()
        {
            using (var disposeScope = ServiceProvider.CreateScope())
            {
                var ctx = disposeScope.ServiceProvider.GetRequiredService<TenantDbContext>();
                ctx.Database.EnsureDeleted();
            }

            _host.DisposeAsync().AsTask().GetAwaiter().GetResult();

            if (File.Exists(_dbPath))
            {
                try { File.Delete(_dbPath); } catch (IOException) { /* best effort cleanup */ }
            }
        }
    }
}
