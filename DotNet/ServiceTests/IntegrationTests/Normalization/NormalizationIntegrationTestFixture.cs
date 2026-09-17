using Azure.Storage.Blobs;
using Confluent.Kafka;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Kafka;
using LantanaGroup.Link.Normalization.Application.Models.Messages;
using LantanaGroup.Link.Normalization.Application.Services;
using LantanaGroup.Link.Normalization.Application.Services.Operations;
using LantanaGroup.Link.Normalization.Domain;
using LantanaGroup.Link.Normalization.Domain.Entities;
using LantanaGroup.Link.Normalization.Domain.Managers;
using LantanaGroup.Link.Normalization.Domain.Queries;
using LantanaGroup.Link.Normalization.Domain.Repositories;
using LantanaGroup.Link.Normalization.Domain.Services;
using LantanaGroup.Link.Normalization.Listeners;
using LantanaGroup.Link.Report.Application.Interfaces;
using LantanaGroup.Link.Report.Application.Options;
using LantanaGroup.Link.Report.Data;
using LantanaGroup.Link.Report.KafkaProducers;
using LantanaGroup.Link.Report.Listeners;
using LantanaGroup.Link.Report.Models;
using LantanaGroup.Link.Report.Services;
using LantanaGroup.Link.Sdk.Clients;
using LantanaGroup.Link.Shared.Application.Error.Interfaces;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using LantanaGroup.Link.Shared.Application.Models.Tenant;
using LantanaGroup.Link.Shared.Application.Services.ResourceCache;
using LantanaGroup.Link.Shared.Domain.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using StackExchange.Redis.Extensions.System.Text.Json;
using System.Resources;
using Testcontainers.Azurite;
using Testcontainers.Redis;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory.Database;
using ResourceType = LantanaGroup.Link.Normalization.Domain.Entities.ResourceType;
using ResourceCacheType = LantanaGroup.Link.Shared.Application.Enums.ResourceCacheType;
using Task = System.Threading.Tasks.Task;

namespace IntegrationTests.Normalization
{
    public class NormalizationIntegrationTestFixture : IAsyncLifetime, IDisposable
    {
        public IServiceProvider ServiceProvider { get; private set; }
        public IServiceScopeFactory ScopeFactory { get; private set; } = null!;
        public Mock<IKafkaConsumerFactory<ResourceKey, ResourcesAcquiredValue>> ResourcesAcquiredConsumerFactoryMock { get; } = new();
        public Mock<ITransientExceptionHandler<ResourcesAcquiredListener, ResourceKey, ResourcesAcquiredValue>> ResourcesAcquiredTransientHandlerMock { get; } = new();
        public Mock<IDeadLetterExceptionHandler<ResourcesAcquiredListener, ResourceKey, ResourcesAcquiredValue>> ResourcesAcquiredDeadLetterHandlerMock { get; } = new();
        public Mock<IDeadLetterExceptionHandler<ResourcesAcquiredListener, ResourceKey, string>> ConsumeExceptionHandlerMock { get; } = new();
        public Mock<IProducer<ResourceKey, ResourcesNormalizedValue>> ResourcesNormalizedProducerMock { get; } = new();
        public Mock<IVendorVersionResolver> VendorVersionResolverMock { get; } = new();

        public string AzuriteConnectionString => _azuriteContainer.GetConnectionString();
        public string RedisConnectionString => _redisContainer.GetConnectionString();

        private IHost _host;
        private readonly AzuriteContainer _azuriteContainer = new AzuriteBuilder()
            .WithImage("mcr.microsoft.com/azure-storage/azurite:latest")
            .WithCommand("--skipApiVersionCheck")
            .Build();
        private readonly RedisContainer _redisContainer = new RedisBuilder()
            .WithImage("redis:latest") 
            .Build();

        private async Task InitializeDatabase(IResourceManager resourceManager)
        {
            await resourceManager.InitializeResources();
        }

        public async Task InitializeAsync()
        {
            await _azuriteContainer.StartAsync();
            await _redisContainer.StartAsync();

            var builder = Host.CreateApplicationBuilder();

            builder.Services.AddStackExchangeRedisExtensions<SystemTextJsonSerializer>(new StackExchange.Redis.Extensions.Core.Configuration.RedisConfiguration
            {
                ConnectionString = _redisContainer.GetConnectionString()
            });

            // Add in-memory with warning suppression
            builder.Services.AddDbContext<NormalizationDbContext>(options =>
            {
                options.UseInMemoryDatabase("NormalizationDatabase");
                options.ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning));
            });

            // Register CopyPropertyOperationService as a singleton and hosted service
            builder.Services.AddSingleton<CopyPropertyOperationService>();
            builder.Services.AddSingleton<CopyLocationOperationService>();
            builder.Services.AddSingleton<CopyLocationAliasToTypeIterativelyOperationService>();
            builder.Services.AddSingleton<CodeMapOperationService>();
            builder.Services.AddSingleton<ConditionalTransformOperationService>();
            builder.Services.AddSingleton<RemoveExtensionsOperationService>();

            // Register other services
            var serviceInformation = new ServiceInformation
            {
                ServiceName = "NormalizationIntegrationTest",
                ServiceConfigName = "NormalizationIntegrationTest",
                Version = "1.0.0-test"
            };

            builder.Services.AddSingleton(serviceInformation);
            builder.Services.AddScoped<IEntityRepository<Operation>, OperationRepository>();
            builder.Services.AddScoped<IEntityRepository<OperationSequence>, OperationSequenceRepository>();
            builder.Services.AddScoped<IEntityRepository<ResourceType>, ResourceTypeRepository>();
            builder.Services.AddScoped<IEntityRepository<OperationResourceType>, OperationResourceTypeRepository>();
            builder.Services.AddScoped<IEntityRepository<VendorVersionOperationPreset>, VendorVersionOperationPresetRepository>();

            builder.Services.AddScoped<LantanaGroup.Link.Normalization.Domain.IDatabase, LantanaGroup.Link.Normalization.Domain.Database>();
            builder.Services.AddScoped<IOperationManager, OperationManager>();
            builder.Services.AddScoped<IResourceManager, LantanaGroup.Link.Normalization.Domain.Managers.ResourceManager>();
            builder.Services.AddScoped<IVendorVersionOperationPresetManager, VendorVersionOperationPresetManager>();
            builder.Services.AddScoped<IOperationQueries, OperationQueries>();
            builder.Services.AddScoped<IOperationSequenceQueries, OperationSequenceQueries>();
            builder.Services.AddScoped<IVendorVersionOperationPresetQueries, VendorVersionOperationPresetQueries>();
            builder.Services.AddScoped<IResourceQueries, ResourceQueries>();
            VendorVersionResolverMock
                .Setup(resolver => resolver.ResolveAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
                .Returns((IEnumerable<Guid> vendorVersionIds, CancellationToken _) =>
                    Task.FromResult<IReadOnlyDictionary<Guid, VendorVersionModel>>(vendorVersionIds
                        .Distinct()
                        .ToDictionary(
                            vendorVersionId => vendorVersionId,
                            vendorVersionId => new VendorVersionModel
                            {
                                Id = vendorVersionId,
                                VendorId = Guid.Empty,
                                Version = "test"
                            })));
            builder.Services.AddSingleton<IVendorVersionResolver>(VendorVersionResolverMock.Object);
            builder.Services.AddSingleton(ResourcesAcquiredConsumerFactoryMock.Object);
            builder.Services.AddSingleton(ResourcesAcquiredDeadLetterHandlerMock.Object);
            builder.Services.AddSingleton(ResourcesAcquiredTransientHandlerMock.Object);
            builder.Services.AddSingleton(ConsumeExceptionHandlerMock.Object);
            builder.Services.AddSingleton(ResourcesNormalizedProducerMock.Object);
            
            builder.Services.AddTransient<ResourcesAcquiredListener>();
            builder.Services.AddSingleton(TimeProvider.System);
            builder.Services.AddTransient<INormalizationServiceMetrics, NormalizationServiceMetrics>();
            builder.Services.Configure<BlobStorageSettings>(builder.Configuration.GetSection("BlobStorage"));
            builder.Services.Configure<ResourceCacheBlobStorageSettings>(opts => opts.ConnectionString = _azuriteContainer.GetConnectionString());
            builder.Services.Configure<ResourceCacheBlobStorageSettings>(opts => opts.BlobContainerName = "cache");
            builder.Services.Configure<ResourceCacheSettings>(opts =>
            {
                opts.Redis.ConnectionString = _redisContainer.GetConnectionString();
                opts.BlobStorage.ConnectionString = _azuriteContainer.GetConnectionString();
                opts.BlobStorage.BlobContainerName = "cache";
            });
            builder.Services.AddKeyedSingleton<IResourceCache, RedisResourceCache>(ResourceCacheType.Redis);
            builder.Services.AddKeyedSingleton<IResourceCache, ABSResourceCache>(ResourceCacheType.ABS);
            builder.Services.AddSingleton<IResourceCache, HybridResourceCache>();
            builder.Services.AddSingleton<IResourceCachePurger, ResourceCachePurger>();
            
            builder.Services.AddMemoryCache();

            var provider = builder.Services.BuildServiceProvider();
            var cache = provider.GetRequiredService<IMemoryCache>();

            _host = builder.Build();
            await _host.StartAsync();

            ServiceProvider = _host.Services;
            ScopeFactory = ServiceProvider.GetRequiredService<IServiceScopeFactory>();

            using var scope = ServiceProvider.CreateScope();
            var resourceManager = scope.ServiceProvider.GetRequiredService<IResourceManager>();
            InitializeDatabase(resourceManager).GetAwaiter().GetResult();
        }

        public async Task DisposeAsync()
        {
            _host.StopAsync().GetAwaiter().GetResult();
            _host.Dispose();
        }

        public void Dispose()
        {
            _host.StopAsync().GetAwaiter().GetResult();
            _host.Dispose();
        }
    }
}
