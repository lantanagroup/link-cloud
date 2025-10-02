using Confluent.Kafka;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Managers;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Kafka;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Queries;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Context;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;
using LantanaGroup.Link.Shared.Domain.Repositories.Implementations;
using LantanaGroup.Link.Shared.Domain.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Moq;

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

        public Mock<IProducer<long, ReadyToAcquire>> ReadyToAcquireProducerMock { get; private set; }
        public Mock<IProducer<string, ResourceAcquired>> ResourceAcquiredProducerMock { get; private set; }

        public DataAcquisitionIntegrationTestFixture()
        {
            ReadyToAcquireProducerMock = new Mock<IProducer<long, ReadyToAcquire>>();
            ResourceAcquiredProducerMock = new Mock<IProducer<string, ResourceAcquired>>();

            _host = Host.CreateDefaultBuilder()
                .ConfigureServices((context, services) =>
                {
                    // Add in-memory database with warning suppression
                    services.AddDbContext<DataAcquisitionDbContext>(options =>
                    {
                        options.UseInMemoryDatabase("TestDatabase");
                        options.ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning));
                    });

                    // Register generic repositories for all required entities
                    services.AddScoped<IEntityRepository<DataAcquisitionLog>, EntityRepository<DataAcquisitionLog, DataAcquisitionDbContext>>();
                    services.AddScoped<IEntityRepository<FhirQueryConfiguration>, EntityRepository<FhirQueryConfiguration, DataAcquisitionDbContext>>();
                    services.AddScoped<IEntityRepository<FhirListConfiguration>, EntityRepository<FhirListConfiguration, DataAcquisitionDbContext>>();
                    services.AddScoped<IEntityRepository<FhirQuery>, EntityRepository<FhirQuery, DataAcquisitionDbContext>>();
                    services.AddScoped<IEntityRepository<ReferenceResources>, EntityRepository<ReferenceResources, DataAcquisitionDbContext>>();
                    services.AddScoped<IEntityRepository<QueryPlan>, EntityRepository<QueryPlan, DataAcquisitionDbContext>>();

                    // Register IDatabase implementation
                    services.AddScoped<LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.IDatabase, LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Database>();

                    // Register managers
                    services.AddScoped<IDataAcquisitionLogManager, DataAcquisitionLogManager>();
                    services.AddScoped<IFhirQueryConfigurationManager, FhirQueryConfigurationManager>();

                    // Register queries
                    services.AddScoped<IDataAcquisitionLogQueries, DataAcquisitionLogQueries>();

                    // Mock Kafka producers for integration tests
                    services.AddSingleton<IProducer<long, ReadyToAcquire>>(ReadyToAcquireProducerMock.Object);
                    services.AddSingleton<IProducer<string, ResourceAcquired>>(ResourceAcquiredProducerMock.Object);
                })
                .Build();

            // Start the host
            _host.StartAsync().GetAwaiter().GetResult();
            ServiceProvider = _host.Services;

            // Ensure database is created
            using var scope = ServiceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();
            dbContext.Database.EnsureCreated();
        }

        public void Dispose()
        {
            _host.StopAsync().GetAwaiter().GetResult();
            _host.Dispose();
        }
    }
}