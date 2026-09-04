using FluentAssertions;
using LantanaGroup.Link.Shared.Application.Extensions;
using LantanaGroup.Link.Shared.Application.Services;
using Microsoft.Extensions.Configuration;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.Shared;

[Trait("Category", "UnitTests")]
public class PipelineAbortRegistryTests
{
    [Fact]
    public async Task Abort_by_facility_is_visible_to_later_checks()
    {
        var registry = new InMemoryPipelineAbortRegistry();
        var facilityId = Guid.NewGuid().ToString();

        (await registry.IsAbortedAsync(facilityId, null)).Should().BeFalse();

        await registry.AbortAsync(facilityId, reportId: null, TimeSpan.FromDays(14));

        (await registry.IsAbortedAsync(facilityId, null)).Should().BeTrue();
        (await registry.IsAbortedAsync(Guid.NewGuid().ToString(), null)).Should().BeFalse();
    }

    [Fact]
    public async Task Abort_by_report_is_visible_even_without_facility()
    {
        var registry = new InMemoryPipelineAbortRegistry();
        var reportId = Guid.NewGuid().ToString();

        await registry.AbortAsync(facilityId: null, reportId, TimeSpan.FromDays(14));

        (await registry.IsAbortedAsync(null, reportId)).Should().BeTrue();
        (await registry.IsAbortedAsync(Guid.NewGuid().ToString(), reportId)).Should().BeTrue();
    }

    [Fact]
    public async Task Clear_removes_report_abort_without_touching_other_reports()
    {
        var registry = new InMemoryPipelineAbortRegistry();
        var reportId = Guid.NewGuid().ToString();
        var other = Guid.NewGuid().ToString();

        await registry.AbortAsync(null, reportId, TimeSpan.FromDays(14));
        await registry.AbortAsync(null, other, TimeSpan.FromDays(14));
        await registry.ClearAsync(null, reportId);

        (await registry.IsAbortedAsync(null, reportId)).Should().BeFalse();
        (await registry.IsAbortedAsync(null, other)).Should().BeTrue();
    }

    [Fact]
    public async Task Blank_ids_are_never_aborted()
    {
        var registry = new InMemoryPipelineAbortRegistry();
        await registry.AbortAsync(" ", " ", TimeSpan.FromDays(1));
        (await registry.IsAbortedAsync(null, null)).Should().BeFalse();
        (await registry.IsAbortedAsync("", "")).Should().BeFalse();
    }

    [Fact]
    public void ApplyRedisPassword_AddsPasswordWhenConnectionStringHasNone()
    {
        var result = PipelineAbortRegistryExtensions.ApplyRedisPassword(
            "redis_cache:6379,abortConnect=false",
            "s3cret");

        result.Should().Contain("password=s3cret");
        result.Should().Contain("redis_cache:6379");
    }

    [Fact]
    public void ApplyRedisPassword_DoesNotOverwriteExistingPassword()
    {
        var result = PipelineAbortRegistryExtensions.ApplyRedisPassword(
            "localhost:6379,password=already-set",
            "other");

        result.Should().Contain("password=already-set");
        result.Should().NotContain("other");
    }

    [Fact]
    public void BuildRedisConfiguration_UsesRedisPasswordSection()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Redis"] = "redis_cache:6379,abortConnect=false",
                ["Redis:Password"] = "from-redis-section"
            })
            .Build();

        var result = PipelineAbortRegistryExtensions.BuildRedisConfiguration(configuration);

        result.Should().Contain("password=from-redis-section");
    }

    [Fact]
    public void BuildRedisConfiguration_FallsBackToResourceCachePassword()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Redis"] = "redis_cache:6379",
                ["ResourceCache:Redis:Password"] = "from-cache"
            })
            .Build();

        var result = PipelineAbortRegistryExtensions.BuildRedisConfiguration(configuration);

        result.Should().Contain("password=from-cache");
    }

    [Fact]
    public void BuildRedisConfiguration_FallsBackToRedisPassEnvName()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Redis"] = "localhost:6379",
                ["REDIS_PASS"] = "from-env"
            })
            .Build();

        var result = PipelineAbortRegistryExtensions.BuildRedisConfiguration(configuration);

        result.Should().Contain("password=from-env");
    }
}
