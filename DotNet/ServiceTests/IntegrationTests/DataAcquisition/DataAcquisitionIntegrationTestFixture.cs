using Confluent.Kafka;
using DataAcquisition.Domain.Application.Queries;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Managers;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Kafka;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Queries;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Services;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Validators;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Context;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;
using LantanaGroup.Link.DataAcquisition.Domain.Settings;
using LantanaGroup.Link.Shared.Application.Extensions;
using LantanaGroup.Link.Shared.Application.Extensions.Caching;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Interfaces.Services.Security.Token;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using LantanaGroup.Link.Shared.Application.Services;
using LantanaGroup.Link.Shared.Application.Services.Security.Token;
using LantanaGroup.Link.Shared.Domain.Repositories.Implementations;
using LantanaGroup.Link.Shared.Domain.Repositories.Interfaces;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Medallion.Threading;
using Moq;
using OpenTelemetry.Trace;
using Testcontainers.MsSql;
using Task = System.Threading.Tasks.Task;

namespace IntegrationTests.DataAcquisition
{
    public class DataAcquisitionIntegrationTestFixture : IAsyncLifetime
    {
        // Pinned to a specific tag (rather than :latest) so that the layer
        // cache in CI is reusable across runs and developer machines pull a
        // single, reproducible image.
        private const string SqlServerImage = "mcr.microsoft.com/mssql/server:2022-CU13-ubuntu-22.04";

        // Setting LINK_TESTS_SQL_CONNECTION_STRING points the fixture at an
        // existing SQL Server (LocalDB, docker, devcontainer, etc.) instead of
        // spinning up a Testcontainer. This drops fixture startup from
        // ~30s to ~2s for the inner-dev loop. Each test class still gets an
        // isolated database so re-runs remain deterministic.
        private static string? ExternalConnectionString =>
            Environment.GetEnvironmentVariable("LINK_TESTS_SQL_CONNECTION_STRING");

        public IServiceProvider ServiceProvider { get; private set; } = default!;
        private IHost? _host;
        private readonly MsSqlContainer? _sqlContainer;
        private string? _testDatabaseName;
        private string? _serverConnectionString;

        public Mock<IProducer<long, ReadyToAcquire>> ReadyToAcquireProducerMock { get; private set; }
        public Mock<IProducer<ResourceKey, ResourcesAcquired>> ResourcesAcquiredProducerMock { get; private set; }
        public Mock<IResourceCache> ResourceCacheMock { get; } = new Mock<IResourceCache>();
        public Mock<IPatientDataService> PatientDataServiceMock { get; } = new Mock<IPatientDataService>();

        public DataAcquisitionIntegrationTestFixture()
        {
            ReadyToAcquireProducerMock = new Mock<IProducer<long, ReadyToAcquire>>();
            ResourcesAcquiredProducerMock = new Mock<IProducer<ResourceKey, ResourcesAcquired>>();

            if (string.IsNullOrWhiteSpace(ExternalConnectionString))
            {
                _sqlContainer = new MsSqlBuilder(SqlServerImage)
                    .Build();
            }
        }

        public async Task InitializeAsync()
        {
            if (_sqlContainer is not null)
            {
                await _sqlContainer.StartAsync();
                _serverConnectionString = _sqlContainer.GetConnectionString();
            }
            else
            {
                _serverConnectionString = ExternalConnectionString!;
            }

            // The default container connection string targets 'master'. Migrations
            // that alter database-level settings (e.g. READ_COMMITTED_SNAPSHOT)
            // cannot run against 'master', so point to a dedicated test database.
            // The unique name allows running against a shared external server
            // without colliding with parallel runs.
            _testDatabaseName = $"DataAcquisitionTest_{Guid.NewGuid():N}";
            var csBuilder = new SqlConnectionStringBuilder(_serverConnectionString)
            {
                InitialCatalog = _testDatabaseName,
                ConnectTimeout = 60
            };
            var connectionString = csBuilder.ConnectionString;

            // Create the test database on the server
            await using (var masterConn = new SqlConnection(_serverConnectionString))
            {
                await masterConn.OpenAsync();
                await using var cmd = masterConn.CreateCommand();
                cmd.CommandText = $"CREATE DATABASE [{_testDatabaseName}];";
                await cmd.ExecuteNonQueryAsync();
            }

            var builder = Host.CreateApplicationBuilder();

            // Get assembly version for ServiceInformation
            var assemblyVersion = Assembly.GetExecutingAssembly()
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? string.Empty;

            builder.SetupServiceInformation(
                "DataAcquisitionService",
                assemblyVersion
            );

            builder.Services.AddDbContext<DataAcquisitionDbContext>(options =>
            {
                options.UseSqlServer(connectionString);
            });

            // Register generic repositories for ALL entities (including the new Location ones)
            builder.Services.AddScoped<IEntityRepository<DataAcquisitionLog>, EntityRepository<DataAcquisitionLog, DataAcquisitionDbContext>>();
            builder.Services.AddScoped<IEntityRepository<FhirQueryConfiguration>, EntityRepository<FhirQueryConfiguration, DataAcquisitionDbContext>>();
            builder.Services.AddScoped<IEntityRepository<FhirListConfiguration>, EntityRepository<FhirListConfiguration, DataAcquisitionDbContext>>();
            builder.Services.AddScoped<IEntityRepository<FhirQuery>, EntityRepository<FhirQuery, DataAcquisitionDbContext>>();
            builder.Services.AddScoped<IEntityRepository<ReferenceResources>, EntityRepository<ReferenceResources, DataAcquisitionDbContext>>();
            builder.Services.AddScoped<IEntityRepository<QueryPlan>, EntityRepository<QueryPlan, DataAcquisitionDbContext>>();
            builder.Services.AddScoped<IEntityRepository<FhirQueryResourceType>, EntityRepository<FhirQueryResourceType, DataAcquisitionDbContext>>();
            builder.Services.AddScoped<IEntityRepository<ResourceReferenceType>, EntityRepository<ResourceReferenceType, DataAcquisitionDbContext>>();
            builder.Services.AddScoped<IEntityRepository<SftpAcquisitionLog>, EntityRepository<SftpAcquisitionLog, DataAcquisitionDbContext>>();
            builder.Services.AddScoped<IEntityRepository<SftpConfiguration>, EntityRepository<SftpConfiguration, DataAcquisitionDbContext>>();
            builder.Services.AddScoped<IEntityRepository<EncounterMapping>, EntityRepository<EncounterMapping, DataAcquisitionDbContext>>();
            builder.Services.AddScoped<IEntityRepository<EncounterLocation>, EntityRepository<EncounterLocation, DataAcquisitionDbContext>>();

            // Register IDatabase implementation
            builder.Services.AddScoped<IDatabase, Database>();

            // OrganizationLocationConfiguration repositories
            builder.Services.AddScoped<IEntityRepository<OrganizationLocationConfiguration>, EntityRepository<OrganizationLocationConfiguration, DataAcquisitionDbContext>>();
            builder.Services.AddScoped<IEntityRepository<OrganizationLocationCondition>, EntityRepository<OrganizationLocationCondition, DataAcquisitionDbContext>>();
            builder.Services.AddScoped<IEntityRepository<OrganizationLocationMapping>, EntityRepository<OrganizationLocationMapping, DataAcquisitionDbContext>>();
            builder.Services.AddScoped<IEntityRepository<EncounterMapping>, EntityRepository<EncounterMapping, DataAcquisitionDbContext>>();
            builder.Services.AddScoped<IEntityRepository<EncounterLocation>, EntityRepository<EncounterLocation, DataAcquisitionDbContext>>();

            // Register IDatabase implementation (it will now receive the new repositories via constructor injection)
            builder.Services.AddScoped<IDatabase, Database>();

            builder.Services.AddScoped<IQueryPlanValidator, QueryPlanValidator>();
            builder.Services.AddScoped<ILocationResolutionValidator, LocationResolutionValidator>();
            builder.Services.AddTransient<IDataAcquisitionLogService, DataAcquisitionLogService>();

            // Register a mock IDistributedSemaphoreProvider that always grants the lock.
            // Integration tests run against a real database but do not need distributed
            // coordination; the mock keeps tests fast and self-contained.
            var semaphoreHandle = new Mock<IDistributedSynchronizationHandle>();
            var semaphore = new Mock<IDistributedSemaphore>();
            var semaphoreProvider = new Mock<IDistributedSemaphoreProvider>();
            semaphore
                .Setup(s => s.TryAcquireAsync(It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
                .Returns(new ValueTask<IDistributedSynchronizationHandle?>(semaphoreHandle.Object));
            semaphoreProvider
                .Setup(p => p.CreateSemaphore(It.IsAny<string>(), It.IsAny<int>()))
                .Returns(semaphore.Object);
            builder.Services.AddSingleton<IDistributedSemaphoreProvider>(semaphoreProvider.Object);

            // Register managers                    
            builder.Services.AddScoped<IQueryPlanManager, QueryPlanManager>();
            builder.Services.AddScoped<IFhirListQueryConfigurationManager, FhirListQueryConfigurationManager>();
            builder.Services.AddScoped<IDataAcquisitionLogManager, DataAcquisitionLogManager>();
            builder.Services.AddScoped<IFhirQueryConfigurationManager, FhirQueryConfigurationManager>();
            builder.Services.AddScoped<IFhirQueryManager, FhirQueryManager>();
            builder.Services.AddScoped<IReferenceResourcesManager, ReferenceResourcesManager>();

            // OrganizationLocationConfiguration manager & queries
            builder.Services.AddScoped<IOrganizationLocationConfigurationManager, OrganizationLocationConfigurationManager>();
            builder.Services.AddScoped<IOrganizationLocationConfigurationQueries, OrganizationLocationConfigurationQueries>();
            builder.Services.AddScoped<IOrganizationLocationMappingManager, OrganizationLocationMappingManager>();
            builder.Services.AddScoped<IOrganizationLocationMappingQueries, OrganizationLocationMappingQueries>();
            builder.Services.AddScoped<IEncounterMappingManager, EncounterMappingManager>();
            builder.Services.AddScoped<IEncounterMappingQueries, EncounterMappingQueries>();
            builder.Services.AddTransient<ILocationMappingService, LocationMappingService>();

            // In-memory cache used by LocationMappingService (read) and invalidated by
            // OrganizationLocationConfigurationManager (write).
            builder.Services.AddMemoryCache();
            builder.Services.AddSingleton<ICacheService, InMemoryCacheService>();

            // Register queries
            builder.Services.AddScoped<IDataAcquisitionLogQueries, DataAcquisitionLogQueries>();
            builder.Services.AddScoped<IDataAcquisitionLogNotesQueries, DataAcquisitionLogNotesQueries>();
            builder.Services.AddScoped<IFhirQueryQueries, FhirQueryQueries>();
            builder.Services.AddScoped<IFhirQueryConfigurationQueries, FhirQueryConfigurationQueries>();
            builder.Services.AddScoped<IFhirQueryListConfigurationQueries, FhirQueryListConfigurationQueries>();
            builder.Services.AddScoped<IQueryPlanQueries, QueryPlanQueries>();
            builder.Services.AddTransient<IReferenceResourcesQueries, ReferenceResourcesQueries>();

            // Mock Kafka producers for integration tests
            builder.Services.AddSingleton<IProducer<long, ReadyToAcquire>>(ReadyToAcquireProducerMock.Object);
            builder.Services.AddSingleton<IProducer<ResourceKey, ResourcesAcquired>>(ResourcesAcquiredProducerMock.Object);
            builder.Services.AddSingleton<IResourceCache>(ResourceCacheMock.Object);

            // AcquisitionProcessorBackgroundService dependencies: the real dependency checker exercises
            // the reportability gate end-to-end; the patient-data service is a shared mock (exposed as
            // PatientDataServiceMock) so tests can assert it is NOT invoked on the NotReportable path; the
            // ReadyToAcquire producer factory only needs to resolve.
            builder.Services.AddScoped<IAcquisitionDependencyChecker, AcquisitionDependencyChecker>();
            builder.Services.AddScoped<IPatientDataService>(_ => PatientDataServiceMock.Object);
            builder.Services.AddSingleton<IKafkaProducerFactory<long, ReadyToAcquire>>(_ => new Mock<IKafkaProducerFactory<long, ReadyToAcquire>>().Object);

            builder.Services.Configure<ServiceRegistry>(options =>
            {
                options.TenantService = new TenantServiceRegistration
                {
                    CheckIfTenantExists = false
                };
            });

            builder.Services.AddTransient<ICreateSystemToken, CreateSystemToken>();
            builder.Services.AddTransient<ITenantApiService, TenantApiService>();

            builder.Services.AddHttpClient();

            builder.Services.Configure<AcquisitionWorkerProcessorSettings>(options =>
            {
                options.MaxConcurrentAcquisitions = 8;
                options.WorkChannelCapacity = 200;
                options.MaxBatchesPerFacilityPerRun = 40;
                options.MaxBatchesFailStalledPerRun = 20;
                options.TimeBudgetPerRunSeconds = 20;
            });

            builder.Services.AddOpenTelemetry()
                .WithTracing(tracerBuilder => tracerBuilder
                    .AddSource(ServiceActivitySource.ServiceName)
                    .SetSampler(new AlwaysOnSampler())
                    .AddConsoleExporter());

            _host = builder.Build();

            // Start the host
            await _host.StartAsync();
            ServiceProvider = _host.Services;

            // Apply migrations to create the SQL Server schema
            using var scope = ServiceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();
            await dbContext.Database.MigrateAsync();
        }

        public async Task DisposeAsync()
        {
            if (_host is not null)
            {
                await _host.StopAsync();
                _host.Dispose();
            }

            if (_sqlContainer is not null)
            {
                await _sqlContainer.DisposeAsync();
            }
            else if (!string.IsNullOrWhiteSpace(_serverConnectionString) && !string.IsNullOrWhiteSpace(_testDatabaseName))
            {
                // Best-effort drop of the per-fixture database when running
                // against an external server.
                try
                {
                    await using var masterConn = new SqlConnection(_serverConnectionString);
                    await masterConn.OpenAsync();
                    await using var cmd = masterConn.CreateCommand();
                    cmd.CommandText =
                        $"ALTER DATABASE [{_testDatabaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; " +
                        $"DROP DATABASE [{_testDatabaseName}];";
                    await cmd.ExecuteNonQueryAsync();
                }
                catch
                {
                    // Ignore cleanup failures - the next run uses a new GUID.
                }
            }
        }
    }
}
