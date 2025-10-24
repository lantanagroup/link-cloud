using Confluent.Kafka;
using DataAcquisition.Domain.Application.Queries;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Managers;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Kafka;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Queries;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Services;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Context;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;
using LantanaGroup.Link.Shared.Application.Interfaces.Services.Security.Token;
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

            _host = Host.CreateDefaultBuilder()
                .ConfigureServices((context, services) =>
                {
                    // Add SQLite database without shared connection
                    services.AddDbContext<DataAcquisitionDbContext>(options =>
                    {
                        options.UseSqlite($"Data Source={_dbPath};");
                    });

                    // Register generic repositories for all required entities
                    services.AddScoped<IEntityRepository<DataAcquisitionLog>, EntityRepository<DataAcquisitionLog, DataAcquisitionDbContext>>();
                    services.AddScoped<IEntityRepository<FhirQueryConfiguration>, EntityRepository<FhirQueryConfiguration, DataAcquisitionDbContext>>();
                    services.AddScoped<IEntityRepository<FhirListConfiguration>, EntityRepository<FhirListConfiguration, DataAcquisitionDbContext>>();
                    services.AddScoped<IEntityRepository<FhirQuery>, EntityRepository<FhirQuery, DataAcquisitionDbContext>>();
                    services.AddScoped<IEntityRepository<ReferenceResources>, EntityRepository<ReferenceResources, DataAcquisitionDbContext>>();
                    services.AddScoped<IEntityRepository<QueryPlan>, EntityRepository<QueryPlan, DataAcquisitionDbContext>>();
                    services.AddTransient<IEntityRepository<FhirQueryResourceType>, EntityRepository<FhirQueryResourceType, DataAcquisitionDbContext>>();
                    services.AddTransient<IEntityRepository<ResourceReferenceType>, EntityRepository<ResourceReferenceType, DataAcquisitionDbContext>>();

                    // Register IDatabase implementation
                    services.AddScoped<LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.IDatabase, LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Database>();

                    services.AddTransient<IDataAcquisitionLogService, DataAcquisitionLogService>();

                    // Register managers                    
                    services.AddScoped<IQueryPlanManager, QueryPlanManager>();
                    services.AddScoped<IFhirListQueryConfigurationManager, FhirListQueryConfigurationManager>();
                    services.AddScoped<IDataAcquisitionLogManager, DataAcquisitionLogManager>();
                    services.AddScoped<IFhirQueryConfigurationManager, FhirQueryConfigurationManager>();

                    // Register queries
                    services.AddScoped<IDataAcquisitionLogQueries, DataAcquisitionLogQueries>();
                    services.AddScoped<IFhirQueryQueries, FhirQueryQueries>();
                    services.AddScoped<IFhirQueryConfigurationQueries, FhirQueryConfigurationQueries>();
                    services.AddScoped<IFhirQueryListConfigurationQueries, FhirQueryListConfigurationQueries>();
                    services.AddScoped<IQueryPlanQueries, QueryPlanQueries>();
                    services.AddTransient<IReferenceResourcesQueries, ReferenceResourcesQueries>();

                    // Mock Kafka producers for integration tests
                    services.AddSingleton<IProducer<long, ReadyToAcquire>>(ReadyToAcquireProducerMock.Object);
                    services.AddSingleton<IProducer<string, ResourceAcquired>>(ResourceAcquiredProducerMock.Object);


                    services.Configure<ServiceRegistry>(options =>
                    {
                        options.TenantService = new TenantServiceRegistration
                        {
                            CheckIfTenantExists = false
                        };
                    });

                    services.AddTransient<ICreateSystemToken, CreateSystemToken>();
                    services.AddTransient<ITenantApiService, TenantApiService>();

                    services.AddHttpClient();

                    services.AddOpenTelemetry()
                        .WithTracing(builder => builder
                            .AddSource(ServiceActivitySource.ServiceName)
                            .SetSampler(new AlwaysOnSampler())  // Add this to force sampling every trace
                            .AddConsoleExporter());
                })
                .Build();

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