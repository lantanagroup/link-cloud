using LantanaGroup.Link.Normalization.Domain;
using LantanaGroup.Link.Normalization.Domain.Entities;
using LantanaGroup.Link.Normalization.Domain.Managers;
using LantanaGroup.Link.Normalization.Domain.Queries;
using LantanaGroup.Link.Normalization.Domain.Repositories;
using LantanaGroup.Link.Shared.Domain.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ResourceType = LantanaGroup.Link.Normalization.Domain.Entities.ResourceType;
using Task = System.Threading.Tasks.Task;

namespace NormalizationTests
{
    [CollectionDefinition("IntegrationTest")]
    public class DatabaseCollection : ICollectionFixture<IntegrationTestFixture>
    {
        // This class is a marker for the collection
    }

    public class IntegrationTestFixture : IDisposable
    {
        public ServiceProvider ServiceProvider { get; private set; }

        public IntegrationTestFixture()
        {
            var services = new ServiceCollection();

            // Configure DbContext (e.g., In-Memory for testing)
            services.AddDbContext<NormalizationDbContext>(options =>
                options.UseInMemoryDatabase("TestDatabase"));

            // Register your data layer services (e.g., repositories)
            services.AddScoped<IEntityRepository<Operation>, OperationRepository>();
            services.AddScoped<IEntityRepository<OperationSequence>, OperationSequenceRepository>();
            services.AddScoped<IEntityRepository<ResourceType>, ResourceTypeRepository>();
            services.AddScoped<IEntityRepository<OperationResourceType>, OperationResourceTypeRepository>();
            services.AddScoped<IDatabase, Database>();
            services.AddScoped<IOperationManager, OperationManager>();
            services.AddScoped<IOperationQueries, OperationQueries>();

            // Build the service provider
            ServiceProvider = services.BuildServiceProvider();

            using var scope = ServiceProvider.CreateScope();
            var database = scope.ServiceProvider.GetRequiredService<IDatabase>();
            InitializeDatabase(database).GetAwaiter().GetResult();
        }

        private async Task InitializeDatabase(IDatabase database)
        {
            // Add required ResourceTypes
            await database.ResourceTypes.AddAsync(new ResourceType { Name = "Location" });
            await database.ResourceTypes.AddAsync(new ResourceType { Name = "Patient" });
            await database.ResourceTypes.AddAsync(new ResourceType { Name = "Observation" });
            await database.ResourceTypes.AddAsync(new ResourceType { Name = "MedicationRequest" });
            await database.SaveChangesAsync();
        }

        public T GetService<T>() => ServiceProvider.GetService<T>();

        public void Dispose()
        {
            ServiceProvider.Dispose();
        }
    }
}
