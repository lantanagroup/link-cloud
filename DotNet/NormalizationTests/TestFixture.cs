using LantanaGroup.Link.Normalization.Application.Operations;
using LantanaGroup.Link.Normalization.Domain;
using LantanaGroup.Link.Normalization.Domain.Entities;
using LantanaGroup.Link.Normalization.Domain.Managers;
using LantanaGroup.Link.Normalization.Domain.Queries;
using LantanaGroup.Link.Normalization.Domain.Repositories;
using LantanaGroup.Link.Shared.Domain.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ResourceType = LantanaGroup.Link.Normalization.Domain.Entities.ResourceType;
using Task = System.Threading.Tasks.Task;

namespace NormalizationTests
{
    [CollectionDefinition("IntegrationTests")]
    public class DatabaseCollection : ICollectionFixture<IntegrationTestFixture>
    {
        // This class is a marker for the collection
    }

    public class IntegrationTestFixture : IDisposable
    {
        public IServiceProvider ServiceProvider { get; private set; }
        private readonly IHost _host;

        public IntegrationTestFixture()
        {
            _host = Host.CreateDefaultBuilder()
                .ConfigureServices((context, services) =>
                {
                    // Configure DbContext (In-Memory for testing)
                    services.AddDbContext<NormalizationDbContext>(options =>
                        options.UseInMemoryDatabase("TestDatabase"));

                    // Register CopyPropertyOperationService as a singleton and hosted service
                    services.AddSingleton<CopyPropertyOperationService>();
                    services.AddHostedService(provider => provider.GetRequiredService<CopyPropertyOperationService>());

                    // Register other services
                    services.AddScoped<IEntityRepository<Operation>, OperationRepository>();
                    services.AddScoped<IEntityRepository<OperationSequence>, OperationSequenceRepository>();
                    services.AddScoped<IEntityRepository<ResourceType>, ResourceTypeRepository>();
                    services.AddScoped<IEntityRepository<OperationResourceType>, OperationResourceTypeRepository>();
                    services.AddScoped<IDatabase, Database>();
                    services.AddScoped<IOperationManager, OperationManager>();
                    services.AddScoped<IOperationQueries, OperationQueries>();
                })
                .Build();

            // Start the host
            _host.StartAsync().GetAwaiter().GetResult();
            ServiceProvider = _host.Services;

            using var scope = ServiceProvider.CreateScope();
            var database = scope.ServiceProvider.GetRequiredService<IDatabase>();
            InitializeDatabase(database).GetAwaiter().GetResult();
        }

        public void Dispose()
        {
            _host.StopAsync().GetAwaiter().GetResult();
            _host.Dispose();
        }

        private async Task InitializeDatabase(IDatabase database)
        {
            // Add required ResourceTypes
            await database.ResourceTypes.AddAsync(new ResourceType { Name = "Location" });
            await database.ResourceTypes.AddAsync(new ResourceType { Name = "Patient" });
            await database.ResourceTypes.AddAsync(new ResourceType { Name = "Observation" });
            await database.ResourceTypes.AddAsync(new ResourceType { Name = "MedicationRequest" });
            await database.ResourceTypes.AddAsync(new ResourceType { Name = "Condition" });
            await database.ResourceTypes.AddAsync(new ResourceType { Name = "AllergyIntolerance" });
            await database.ResourceTypes.AddAsync(new ResourceType { Name = "DiagnosticReport" });
            await database.ResourceTypes.AddAsync(new ResourceType { Name = "Encounter" });
            await database.SaveChangesAsync();
        }
    }
}