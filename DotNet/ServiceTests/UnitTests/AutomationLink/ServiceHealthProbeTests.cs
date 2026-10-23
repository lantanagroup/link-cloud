
using FluentAssertions;
using LantanaGroup.Automation.Helpers;
using LantanaGroup.Link.Automation.Link.Helpers;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.AutomationLink;

[Trait("Category", "UnitTests")]
public class ServiceHealthProbeTests
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan OutageThreshold = TimeSpan.FromSeconds(120);

    [Fact]
    public async Task HealthyService_DoesNotTriggerCriticalFailure()
    {
        var clock = new TestTimeProvider();
        var probe = CreateProbe(() => true, clock);

        var result = await ExecuteAsync(probe);

        result.HasCriticalFailure.Should().NotBe(true);
    }

    [Fact]
    public async Task UnhealthyBelowThreshold_DoesNotTriggerCriticalFailure()
    {
        var clock = new TestTimeProvider();
        var probe = CreateProbe(() => false, clock);

        (await ExecuteAsync(probe)).HasCriticalFailure.Should().NotBe(true);

        clock.Advance(TimeSpan.FromSeconds(119));

        var result = await ExecuteAsync(probe);

        result.HasCriticalFailure.Should().NotBe(true);
    }

    [Fact]
    public async Task UnhealthyAtThreshold_TriggersCriticalFailure()
    {
        var clock = new TestTimeProvider();
        var probe = CreateProbe(() => false, clock);

        await ExecuteAsync(probe);

        clock.Advance(OutageThreshold);

        var result = await ExecuteAsync(probe);

        result.HasCriticalFailure.Should().BeTrue();
        result.Issues.Should().ContainSingle(issue =>
            issue.Key == "service-health-timeout" &&
            issue.Source == "ServiceHealth" &&
            issue.Severity == MonitorIssueSeverity.Critical);
    }

    [Fact]
    public async Task RecoveryBeforeThreshold_ResetsOutageTimer()
    {
        var clock = new TestTimeProvider();
        bool? healthy = false;
        var probe = CreateProbe(() => healthy, clock);

        await ExecuteAsync(probe);

        clock.Advance(TimeSpan.FromSeconds(100));
        healthy = true;
        (await ExecuteAsync(probe)).HasCriticalFailure.Should().NotBe(true);

        clock.Advance(TimeSpan.FromSeconds(30));
        healthy = false;
        (await ExecuteAsync(probe)).HasCriticalFailure.Should().NotBe(true);

        clock.Advance(TimeSpan.FromSeconds(100));
        (await ExecuteAsync(probe)).HasCriticalFailure.Should().NotBe(true);
    }

    [Fact]
    public async Task SecondOutage_UsesNewThresholdWindow()
    {
        var clock = new TestTimeProvider();
        bool? healthy = false;
        var probe = CreateProbe(() => healthy, clock);

        await ExecuteAsync(probe);

        clock.Advance(TimeSpan.FromSeconds(100));
        healthy = true;
        await ExecuteAsync(probe);

        clock.Advance(TimeSpan.FromSeconds(5));
        healthy = false;
        await ExecuteAsync(probe);

        clock.Advance(OutageThreshold);

        (await ExecuteAsync(probe)).HasCriticalFailure.Should().BeTrue();
    }

    [Fact]
    public async Task UnknownHealth_DoesNotTriggerCriticalFailure()
    {
        var clock = new TestTimeProvider();
        bool? healthy = null;
        var probe = CreateProbe(() => healthy, clock);

        await ExecuteAsync(probe);

        clock.Advance(TimeSpan.FromMinutes(10));

        var result = await ExecuteAsync(probe);

        result.HasCriticalFailure.Should().NotBe(true);
    }

    [Fact]
    public async Task UnknownHealth_ResetsPreviousOutageWindow()
    {
        var clock = new TestTimeProvider();
        bool? healthy = false;
        var probe = CreateProbe(() => healthy, clock);

        await ExecuteAsync(probe);

        clock.Advance(TimeSpan.FromSeconds(100));
        healthy = null;
        await ExecuteAsync(probe);

        clock.Advance(TimeSpan.FromSeconds(30));
        healthy = false;
        await ExecuteAsync(probe);

        clock.Advance(TimeSpan.FromSeconds(100));

        (await ExecuteAsync(probe)).HasCriticalFailure.Should().NotBe(true);
    }

    [Fact]
    public void NullHealthCheck_ThrowsArgumentNullException()
    {
        var action = () => new ServiceHealthProbe(
            null!,
            CheckInterval,
            OutageThreshold);

        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ZeroCheckInterval_ThrowsArgumentOutOfRangeException()
    {
        var action = () => new ServiceHealthProbe(
            _ => Task.FromResult<bool?>(true),
            TimeSpan.Zero,
            OutageThreshold);

        action.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void ZeroOutageThreshold_ThrowsArgumentOutOfRangeException()
    {
        var action = () => new ServiceHealthProbe(
            _ => Task.FromResult<bool?>(true),
            CheckInterval,
            TimeSpan.Zero);

        action.Should().Throw<ArgumentOutOfRangeException>();
    }

    private static ServiceHealthProbe CreateProbe(
        Func<bool?> healthCheck,
        TimeProvider clock)
    {
        return new ServiceHealthProbe(
            _ => Task.FromResult(healthCheck()),
            CheckInterval,
            OutageThreshold,
            clock);
    }

    private static async Task<MonitorProbeResult> ExecuteAsync(ServiceHealthProbe probe)
    {
        return await probe.ExecuteAsync(null!, CancellationToken.None);
    }

    private sealed class TestTimeProvider : TimeProvider
    {
        private DateTimeOffset _utcNow =
            new(2026, 10, 23, 12, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => _utcNow;

        public void Advance(TimeSpan duration)
        {
            _utcNow = _utcNow.Add(duration);
        }
    }
}
