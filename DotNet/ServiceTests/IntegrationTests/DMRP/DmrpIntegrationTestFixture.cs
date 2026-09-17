using LantanaGroup.Link.DMRP.Business;
using LantanaGroup.Link.DMRP.DependencyInjection;
using LantanaGroup.Link.Shared.Application.Extensions;
using LantanaGroup.Link.Shared.Application.Models.Tenant;
using LantanaGroup.Link.Shared.Domain.Repositories.Interceptors;
using LantanaGroup.Link.Tenant.Repository.Context;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using System.Reflection;
using Task = System.Threading.Tasks.Task;

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

        /// <summary>
        /// Stands in for the host's facility operations, which the module puts its own behavior in front
        /// of. Tests that care what the host was asked to do set this up; the rest leave it with its
        /// default behavior.
        /// </summary>
        public Mock<IFacilityOperations> FacilityOperationsMock { get; } = new();

        /// <summary>
        /// The named type the module wraps. It forwards to <see cref="FacilityOperationsMock"/>, which a
        /// generic type argument cannot name.
        /// </summary>
        public sealed class HostFacilityOperations : IFacilityOperations
        {
            private readonly IFacilityOperations _mock;

            public HostFacilityOperations(IFacilityOperations mock) => _mock = mock;

            public Task CreateAsync(FacilityModel facility, CancellationToken cancellationToken = default) =>
                _mock.CreateAsync(facility, cancellationToken);

            public Task UpdateAsync(FacilityModel existingFacility, FacilityModel updatedFacility,
                CancellationToken cancellationToken = default) =>
                _mock.UpdateAsync(existingFacility, updatedFacility, cancellationToken);

            public Task DeleteAsync(string facilityId, CancellationToken cancellationToken = default) =>
                _mock.DeleteAsync(facilityId, cancellationToken);

            public Task SoftDeleteAsync(string facilityId, CancellationToken cancellationToken = default) =>
                _mock.SoftDeleteAsync(facilityId, cancellationToken);

            public Task RestoreAsync(FacilityModel facility, CancellationToken cancellationToken = default) =>
                _mock.RestoreAsync(facility, cancellationToken);
        }

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

            // The module puts its own behavior in front of the host's facility operations rather than
            // supplying them, so the fixture stands in for the host here as it does for the facility
            // lookup above. The module needs a named type, so the mock is reached through one.
            builder.Services.AddScoped(_ => new HostFacilityOperations(FacilityOperationsMock.Object));

            var registered = builder.AddDmrpModule<TenantDbContext, HostFacilityOperations>(
                builder.Services.AddControllers());
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
        /// Restores the default "every facility exists" stub, dropping any setup a test added to the
        /// shared mock.
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
