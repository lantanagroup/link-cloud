using Automation.UI.Services;
using FluentAssertions;
using Moq;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.AutomationUI;

[Trait("Category", "UnitTests")]
public class LiveProcessUtilizationServiceTests
{
    [Fact]
    public void Task_manager_percent_is_cores_over_logical_processors()
    {
        LiveProcessUtilizationService.ToTaskManagerPercent(0.4, 24).Should().BeApproximately(1.666, 0.001);
        LiveProcessUtilizationService.ToTaskManagerPercent(24, 24).Should().Be(100);
        LiveProcessUtilizationService.ToTaskManagerPercent(1, 0).Should().Be(0);
    }

    [Fact]
    public async Task Returns_unreachable_without_querying_when_prometheus_is_down()
    {
        var prom = new Mock<IPrometheusHistogramClient>(MockBehavior.Strict);
        prom.Setup(p => p.IsReachableAsync(It.IsAny<CancellationToken>())).ReturnsAsync(false);
        var service = new LiveProcessUtilizationService(prom.Object);

        var result = await service.GetAsync();

        result.Reachable.Should().BeFalse();
        result.Services.Should().BeEmpty();
        prom.Verify(p => p.QueryVectorAsync(It.IsAny<string>(), It.IsAny<DateTimeOffset?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Merges_dotnet_and_jvm_series_and_puts_pipeline_services_first()
    {
        var prom = new Mock<IPrometheusHistogramClient>();
        prom.Setup(p => p.IsReachableAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        prom.Setup(p => p.QueryVectorAsync(It.IsAny<string>(), It.IsAny<DateTimeOffset?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string query, DateTimeOffset? _, CancellationToken _) =>
            {
                if (query.Contains("process_memory_usage_bytes", StringComparison.Ordinal))
                    return
                    [
                        new PromSample("Tenant", 100_000_000),
                        new PromSample("Normalization", 500_000_000)
                    ];
                if (query.Contains("process_cpu_time_seconds_total", StringComparison.Ordinal))
                    return
                    [
                        new PromSample("Normalization", 0.4),
                        new PromSample("Tenant", 0.05)
                    ];
                if (query.Contains("process_cpu_count", StringComparison.Ordinal))
                    return
                    [
                        new PromSample("Normalization", 24),
                        new PromSample("Tenant", 24)
                    ];
                if (query.Contains("jvm_memory_used_bytes", StringComparison.Ordinal))
                    return [new PromSample("measureeval", 300_000_000)];
                if (query.Contains("jvm_cpu_recent_utilization_ratio", StringComparison.Ordinal))
                    return [new PromSample("measureeval", 0.02)];
                if (query.Contains("jvm_cpu_count", StringComparison.Ordinal))
                    return [new PromSample("measureeval", 24)];
                return [];
            });

        var result = await new LiveProcessUtilizationService(prom.Object).GetAsync();

        result.Reachable.Should().BeTrue();
        result.Services.Select(s => s.Key).Should().Equal("Normalization", "measureeval", "Tenant");
        result.Services[0].Group.Should().Be("pipeline");
        result.Services[0].MemoryBytes.Should().Be(500_000_000);
        result.Services[0].CpuCores.Should().Be(0.4);
        result.Services[0].CpuPercent.Should().BeApproximately(100.0 * 0.4 / 24, 0.001);
        result.Services[1].Name.Should().Be("Measure Evaluation");
        result.Services[1].CpuCores.Should().BeApproximately(0.48, 0.001);
        result.Services[1].CpuPercent.Should().BeApproximately(2.0, 0.001);
        result.Services[2].Group.Should().Be("platform");
    }
}

