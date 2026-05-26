using LantanaGroup.Link.Normalization.Application.Services.Operations;
using LantanaGroup.Link.Normalization.Domain;
using LantanaGroup.Link.Normalization.Domain.Entities;
using LantanaGroup.Link.Normalization.Domain.Managers;
using LantanaGroup.Link.Normalization.Domain.Queries;
using LantanaGroup.Link.Normalization.Domain.Repositories;
using LantanaGroup.Link.Shared.Domain.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ResourceType = LantanaGroup.Link.Normalization.Domain.Entities.ResourceType;
using Task = System.Threading.Tasks.Task;

namespace IntegrationTests.Normalization
{
    public class NormalizationIntegrationTestFixture : IDisposable
    {
        public IServiceProvider ServiceProvider { get; private set; }
        private readonly IHost _host;

        public NormalizationIntegrationTestFixture()
        {
            _host = Host.CreateDefaultBuilder()
                .ConfigureServices((context, services) =>
                {
                    // Add in-memory with warning suppression
                    services.AddDbContext<NormalizationDbContext>(options =>
                    {
                        options.UseInMemoryDatabase("NormalizationDatabase");
                        options.ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning));
                    });

                    // Register CopyPropertyOperationService as a singleton and hosted service
                    services.AddSingleton<CopyPropertyOperationService>();
                    services.AddSingleton<CopyLocationOperationService>();
                    services.AddSingleton<CodeMapOperationService>();
                    services.AddSingleton<ConditionalTransformOperationService>();

                    // Register other services
                    services.AddScoped<IEntityRepository<Operation>, OperationRepository>();
                    services.AddScoped<IEntityRepository<OperationSequence>, OperationSequenceRepository>();
                    services.AddScoped<IEntityRepository<ResourceType>, ResourceTypeRepository>();
                    services.AddScoped<IEntityRepository<OperationResourceType>, OperationResourceTypeRepository>();
                    services.AddScoped<IEntityRepository<Vendor>, VendorRepository>();
                    services.AddScoped<IEntityRepository<VendorVersion>, VendorVersionRepository>();
                    services.AddScoped<IEntityRepository<VendorVersionOperationPreset>, VendorVersionOperationPresetRepository>();

                    services.AddScoped<IDatabase, Database>();

                    services.AddScoped<IOperationManager, OperationManager>();
                    services.AddScoped<IResourceManager, ResourceManager>();
                    services.AddScoped<IVendorManager, VendorManager>();

                    services.AddScoped<IOperationQueries, OperationQueries>();
                    services.AddScoped<IOperationSequenceQueries, OperationSequenceQueries>();
                    services.AddScoped<IVendorQueries, VendorQueries>();
                    services.AddScoped<IResourceQueries, ResourceQueries>();

                    services.AddMemoryCache();

                    var provider = services.BuildServiceProvider();
                    var cache = provider.GetRequiredService<IMemoryCache>();
                })
                .Build();

            // Start the host
            _host.StartAsync().GetAwaiter().GetResult();
            ServiceProvider = _host.Services;

            using var scope = ServiceProvider.CreateScope();
            var resourceManager = scope.ServiceProvider.GetRequiredService<IResourceManager>();
            InitializeDatabase(resourceManager).GetAwaiter().GetResult();
        }

        public void Dispose()
        {
            _host.StopAsync().GetAwaiter().GetResult();
            _host.Dispose();
        }

        private async Task InitializeDatabase(IResourceManager resourceManager)
        {
            await resourceManager.InitializeResources();
        }
    }
}