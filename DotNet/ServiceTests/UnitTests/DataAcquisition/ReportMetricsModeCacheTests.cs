using Confluent.Kafka;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Services;
using LantanaGroup.Link.Shared.Application.Extensions.Caching;
using LantanaGroup.Link.Shared.Application.Utilities;
using Microsoft.Extensions.Caching.Memory;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.DataAcquisition;

[Trait("Category", "UnitTests")]
public class ReportMetricsModeCacheTests
{
    [Fact]
    public async Task RememberIfPerformance_round_trips_facility_and_report()
    {
        var cache = new InMemoryCacheService(new MemoryCache(new MemoryCacheOptions { SizeLimit = 64 }));
        var headers = new Headers();
        KafkaHeaderHelper.ApplyIfPerformance(headers, "performance");

        await ReportMetricsModeCache.RememberIfPerformanceAsync(cache, "fac-a", "rpt-1", headers);
        var mode = await ReportMetricsModeCache.TryGetAsync(cache, "fac-a", "rpt-1");

        Assert.Equal("performance", mode);
        Assert.Null(await ReportMetricsModeCache.TryGetAsync(cache, "fac-a", "other"));
    }

    [Fact]
    public async Task RememberIfPerformance_ignores_lightweight_headers()
    {
        var cache = new InMemoryCacheService(new MemoryCache(new MemoryCacheOptions { SizeLimit = 64 }));
        await ReportMetricsModeCache.RememberIfPerformanceAsync(cache, "fac-a", "rpt-1", new Headers());
        Assert.Null(await ReportMetricsModeCache.TryGetAsync(cache, "fac-a", "rpt-1"));
    }
}
