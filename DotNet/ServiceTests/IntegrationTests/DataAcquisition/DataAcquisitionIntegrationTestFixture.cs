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
using Moq;
using OpenTelemetry.Trace;
using Testcontainers.MsSql;
using Task = System.Threading.Tasks.Task;

namespace IntegrationTests.DataAcquisition
{
    [CollectionDefinition("DataAcquisitionIntegrationTests", DisableParallelization = true)]
    public class DatabaseCollection : ICollectionFixture<DataAcquisitionIntegrationTestFixture>
    {
        // This class is a marker for the collection
    }

    public class DataAcquisitionIntegrationTestFixture : IAsyncLifetime
    {
        public IServiceProvider ServiceProvider { get; private set; } = default!;
        private IHost? _host;
        private readonly MsSqlContainer _sqlContainer;

        public Mock<IProducer<long, ReadyToAcquire>> ReadyToAcquireProducerMock { get; private set; }
        public Mock<IProducer<ResourceKey, ResourceAcquired>> ResourceAcquiredProducerMock { get; private set; }

        public DataAcquisitionIntegrationTestFixture()
        {
            ReadyToAcquireProducerMock = new Mock<IProducer<long, ReadyToAcquire>>();
            ResourceAcquiredProducerMock = new Mock<IProducer<ResourceKey, ResourceAcquired>>();

            _sqlContainer = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest")
                .Build();
        }

        public async Task InitializeAsync()
        {
            await _sqlContainer.StartAsync();

            // The container's default connection string targets 'master'.
            // Migrations that alter database-level settings (e.g. READ_COMMITTED_SNAPSHOT)
            // cannot run against 'master', so point to a dedicated test database.
            var csBuilder = new SqlConnectionStringBuilder(_sqlContainer.GetConnectionString())
            {
                InitialCatalog = "DataAcquisitionTest",
                ConnectTimeout = 60
            };
            var connectionString = csBuilder.ConnectionString;

            // Create the test database on the container
            await using (var masterConn = new SqlConnection(_sqlContainer.GetConnectionString()))
            {
                await masterConn.OpenAsync();
                await using var cmd = masterConn.CreateCommand();
                cmd.CommandText = "CREATE DATABASE [DataAcquisitionTest];";
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
            builder.Services.AddTransient<IDataAcquisitionLogService, DataAcquisitionLogService>();

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
            builder.Services.AddSingleton<IProducer<ResourceKey, ResourceAcquired>>(ResourceAcquiredProducerMock.Object);

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

            await _sqlContainer.DisposeAsync();
        }
    }
}