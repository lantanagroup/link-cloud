using Confluent.Kafka;
using DataAcquisition.Domain.Application.Queries;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Managers;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Kafka;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Queries;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Services;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Validators;
using LantanaGroup.Link.DataAcquisition.Domain.Settings;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Context;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;
using LantanaGroup.Link.Shared.Application.Extensions;
using LantanaGroup.Link.Shared.Application.Interfaces.Services.Security.Token;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.Services;
using LantanaGroup.Link.Shared.Application.Services.Security.Token;
using LantanaGroup.Link.Shared.Domain.Repositories.Implementations;
using LantanaGroup.Link.Shared.Domain.Repositories.Interfaces;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Moq;
using OpenTelemetry.Trace;

namespace IntegrationTests.DataAcquisition
{
    [CollectionDefinition("DataAcquisitionIntegrationTests", DisableParallelization = true)]
    public class DatabaseCollection : ICollectionFixture<DataAcquisitionIntegrationTestFixture>
    {
        // This class is a marker for the collection
    }

    public class DataAcquisitionIntegrationTestFixture : IDisposable
    {
        public IServiceProvider ServiceProvider { get; private set; }
        private readonly IHost _host;
        private readonly string _dbPath;

        public Mock<IProducer<long, ReadyToAcquire>> ReadyToAcquireProducerMock { get; private set; }
        public Mock<IProducer<string, ResourceAcquired>> ResourceAcquiredProducerMock { get; private set; }

        public DataAcquisitionIntegrationTestFixture()
        {
            ReadyToAcquireProducerMock = new Mock<IProducer<long, ReadyToAcquire>>();
            ResourceAcquiredProducerMock = new Mock<IProducer<string, ResourceAcquired>>();

            _dbPath = Path.Combine(Path.GetTempPath(), $"testdb_{Guid.NewGuid()}.db");
            var sqliteConnectionString = $"Data Source={_dbPath};";

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
                options.UseSqlite(sqliteConnectionString);
            });

            // Register generic repositories for ALL entities (including the new Location ones)
            builder.Services.AddScoped<IEntityRepository<DataAcquisitionLog>, EntityRepository<DataAcquisitionLog, DataAcquisitionDbContext>>();
            builder.Services.AddScoped<IEntityRepository<FhirQueryConfiguration>, EntityRepository<FhirQueryConfiguration, DataAcquisitionDbContext>>();
            builder.Services.AddScoped<IEntityRepository<FhirListConfiguration>, EntityRepository<FhirListConfiguration, DataAcquisitionDbContext>>();
            builder.Services.AddScoped<IEntityRepository<FhirQuery>, EntityRepository<FhirQuery, DataAcquisitionDbContext>>();
            builder.Services.AddScoped<IEntityRepository<ReferenceResources>, EntityRepository<ReferenceResources, DataAcquisitionDbContext>>();
            builder.Services.AddScoped<IEntityRepository<QueryPlan>, EntityRepository<QueryPlan, DataAcquisitionDbContext>>();
            builder.Services.AddTransient<IEntityRepository<FhirQueryResourceType>, EntityRepository<FhirQueryResourceType, DataAcquisitionDbContext>>();
            builder.Services.AddTransient<IEntityRepository<ResourceReferenceType>, EntityRepository<ResourceReferenceType, DataAcquisitionDbContext>>();

            // NEW: OrganizationLocationConfiguration repositories
            builder.Services.AddScoped<IEntityRepository<OrganizationLocationConfiguration>, EntityRepository<OrganizationLocationConfiguration, DataAcquisitionDbContext>>();
            builder.Services.AddScoped<IEntityRepository<OrganizationLocationCondition>, EntityRepository<OrganizationLocationCondition, DataAcquisitionDbContext>>();

            // Register IDatabase implementation (it will now receive the new repositories via constructor injection)
            builder.Services.AddScoped<IDatabase, Database>();

            builder.Services.AddScoped<IQueryPlanValidator, QueryPlanValidator>();
            builder.Services.AddTransient<IDataAcquisitionLogService, DataAcquisitionLogService>();

            // Register managers
            builder.Services.AddScoped<IQueryPlanManager, QueryPlanManager>();
            builder.Services.AddScoped<IFhirListQueryConfigurationManager, FhirListQueryConfigurationManager>();
            builder.Services.AddScoped<IDataAcquisitionLogManager, DataAcquisitionLogManager>();
            builder.Services.AddScoped<IFhirQueryConfigurationManager, FhirQueryConfigurationManager>();

            // NEW: OrganizationLocationConfiguration manager & queries
            builder.Services.AddScoped<IOrganizationLocationConfigurationManager, OrganizationLocationConfigurationManager>();
            builder.Services.AddScoped<IOrganizationLocationConfigurationQueries, OrganizationLocationConfigurationQueries>();

            // Register queries
            builder.Services.AddScoped<IDataAcquisitionLogQueries, DataAcquisitionLogQueries>();
            builder.Services.AddScoped<IFhirQueryQueries, FhirQueryQueries>();
            builder.Services.AddScoped<IFhirQueryConfigurationQueries, FhirQueryConfigurationQueries>();
            builder.Services.AddScoped<IFhirQueryListConfigurationQueries, FhirQueryListConfigurationQueries>();
            builder.Services.AddScoped<IQueryPlanQueries, QueryPlanQueries>();
            builder.Services.AddTransient<IReferenceResourcesQueries, ReferenceResourcesQueries>();

            // Mock Kafka producers for integration tests
            builder.Services.AddSingleton<IProducer<long, ReadyToAcquire>>(ReadyToAcquireProducerMock.Object);
            builder.Services.AddSingleton<IProducer<string, ResourceAcquired>>(ResourceAcquiredProducerMock.Object);

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
            _host.StartAsync().GetAwaiter().GetResult();
            ServiceProvider = _host.Services;

            // Ensure database is created and set PRAGMAs
            using var scope = ServiceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();
            dbContext.Database.EnsureCreated();

            // Set PRAGMAs
            dbContext.Database.OpenConnection();
            using var cmd = dbContext.Database.GetDbConnection().CreateCommand();
            cmd.CommandText = "PRAGMA journal_mode = WAL;";
            cmd.ExecuteNonQuery();
            cmd.CommandText = "PRAGMA busy_timeout = 5000;";
            cmd.ExecuteNonQuery();
            dbContext.Database.CloseConnection();
        }

        public void Dispose()
        {
            _host.StopAsync().GetAwaiter().GetResult();
            _host.Dispose();
            SqliteConnection.ClearAllPools();
            if (File.Exists(_dbPath)) File.Delete(_dbPath);
        }
    }
}