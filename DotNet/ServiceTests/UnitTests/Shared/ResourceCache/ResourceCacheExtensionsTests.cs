using FluentAssertions;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Extensions;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using Medallion.Threading;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis.Extensions.Core.Abstractions;

namespace UnitTests.Shared.ResourceCache;

[Trait("Category", "UnitTests")]
public class ResourceCacheExtensionsTests
{
    [Fact]
    public void AddResourceCache_UsesExistingRedisConnection_ForHybridCache()
    {
        var services = new ServiceCollection();
        var configuration = CreateConfiguration(ResourceCacheType.Hybrid, includeResourceCacheRedisSettings: false);

        DistributedLockSettingsExtensions.DistributedLockBuildAndAddToDI(services, configuration, "Redis");
        services.AddResourceCache(configuration, useExistingRedisConnection: true);

        services.Count(descriptor => descriptor.ServiceType == typeof(IRedisDatabase)).Should().Be(1);
        services.Should().ContainSingle(descriptor => descriptor.ServiceType == typeof(IDistributedSemaphoreProvider));
        services.Should().ContainSingle(descriptor =>
            descriptor.ServiceType == typeof(IResourceCache) && descriptor.ServiceKey == null);
        services.Should().ContainSingle(descriptor =>
            descriptor.ServiceType == typeof(IResourceCache) && Equals(descriptor.ServiceKey, ResourceCacheType.Redis));
        services.Should().ContainSingle(descriptor =>
            descriptor.ServiceType == typeof(IResourceCache) && Equals(descriptor.ServiceKey, ResourceCacheType.ABS));
    }

    [Fact]
    public void AddResourceCache_RegistersRedisConnection_ForStandaloneCache()
    {
        var services = new ServiceCollection();
        var configuration = CreateConfiguration(ResourceCacheType.Redis);

        services.AddResourceCache(configuration);

        services.Count(descriptor => descriptor.ServiceType == typeof(IRedisDatabase)).Should().Be(1);
        services.Should().ContainSingle(descriptor => descriptor.ServiceType == typeof(IResourceCache));
    }

    [Fact]
    public void AddResourceCache_Throws_WhenExistingRedisConnectionIsMissing()
    {
        var services = new ServiceCollection();
        var configuration = CreateConfiguration(ResourceCacheType.Redis, includeResourceCacheRedisSettings: false);

        var action = () => services.AddResourceCache(configuration, useExistingRedisConnection: true);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*IRedisDatabase registration is required*");
    }

    [Fact]
    public void AddResourceCache_DoesNotRegisterAnotherRedisConnection_ForAbsCache()
    {
        var services = new ServiceCollection();
        var configuration = CreateConfiguration(ResourceCacheType.ABS, includeResourceCacheRedisSettings: false);

        DistributedLockSettingsExtensions.DistributedLockBuildAndAddToDI(services, configuration, "Redis");
        services.AddResourceCache(configuration, useExistingRedisConnection: true);

        services.Count(descriptor => descriptor.ServiceType == typeof(IRedisDatabase)).Should().Be(1);
        services.Should().ContainSingle(descriptor => descriptor.ServiceType == typeof(IDistributedSemaphoreProvider));
        services.Should().ContainSingle(descriptor => descriptor.ServiceType == typeof(IResourceCache));
    }

    private static IConfiguration CreateConfiguration(
        ResourceCacheType cacheImplementation,
        bool includeResourceCacheRedisSettings = true)
    {
        var settings = new Dictionary<string, string?>
        {
            ["ConnectionStrings:Redis"] = "localhost:6379",
            ["Redis:Password"] = "redis-password",
            ["DistributedLockSettings:PoolSize"] = "3",
            ["ResourceCache:CacheImplementation"] = cacheImplementation.ToString(),
            ["ResourceCache:BlobStorage:ConnectionString"] = "UseDevelopmentStorage=true"
        };

        if (includeResourceCacheRedisSettings)
        {
            settings["ResourceCache:Redis:ConnectionString"] = "localhost:6379";
            settings["ResourceCache:Redis:Password"] = "redis-password";
        }

        return new ConfigurationBuilder()
            .AddInMemoryCollection(settings)
            .Build();
    }
}