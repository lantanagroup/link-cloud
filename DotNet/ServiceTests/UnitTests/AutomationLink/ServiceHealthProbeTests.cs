
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

        bool? healthy = true;
        var probe = CreateProbe(() => healthy, clock);

        // Establish initial healthy state.
        await ExecuteAsync(probe);

        // Begin the outage.
        healthy = false;
        await ExecuteAsync(probe);

        // Remain just below the configured threshold.
        clock.Advance(OutageThreshold - TimeSpan.FromSeconds(1));

        var result = await ExecuteAsync(probe);

        result.HasCriticalFailure.Should().BeFalse();
    }

    [Fact]
    public async Task UnhealthyAtThreshold_TriggersCriticalFailure()
    {
        var clock = new TestTimeProvider();

        bool? healthy = true;
        var probe = CreateProbe(() => healthy, clock);

        // Establish that the service was initially healthy.
        await ExecuteAsync(probe);

        // Simulate the service becoming unavailable.
        healthy = false;
        await ExecuteAsync(probe);

        // Advance to the configured outage threshold.
        clock.Advance(OutageThreshold);

        var result = await ExecuteAsync(probe);

        result.HasCriticalFailure.Should().BeTrue();

        result.Issues.Should().ContainSingle(issue =>
            issue.Key == "service-health-timeout:Terminology" &&
            issue.Source == "ServiceHealth" &&
            issue.Severity == MonitorIssueSeverity.Critical);
    }

    [Fact]
    public async Task RecoveryBeforeThreshold_ResetsOutageTimer()
    {
        var clock = new TestTimeProvider();

        bool? healthy = true;
        var probe = CreateProbe(() => healthy, clock);

        // Establish initial healthy state.
        await ExecuteAsync(probe);

        // First outage begins.
        healthy = false;
        await ExecuteAsync(probe);

        clock.Advance(TimeSpan.FromSeconds(100));

        // Service recovers before reaching the threshold.
        healthy = true;

        var recoveryResult = await ExecuteAsync(probe);

        recoveryResult.HasCriticalFailure.Should().BeFalse();

        recoveryResult.Issues.Should().ContainSingle(issue =>
            issue.Key == "service-health-recovered:Terminology");

        // Start a second outage.
        clock.Advance(TimeSpan.FromSeconds(30));

        healthy = false;
        await ExecuteAsync(probe);

        // The second outage must have its own threshold window.
        clock.Advance(TimeSpan.FromSeconds(100));

        var result = await ExecuteAsync(probe);

        result.HasCriticalFailure.Should().BeFalse();
    }

    [Fact]
    public async Task SecondOutage_UsesNewThresholdWindow()
    {
        var clock = new TestTimeProvider();

        bool? healthy = true;
        var probe = CreateProbe(() => healthy, clock);

        // Establish initial healthy state.
        await ExecuteAsync(probe);

        // First outage.
        healthy = false;
        await ExecuteAsync(probe);

        clock.Advance(TimeSpan.FromSeconds(100));

        // Recover before the first outage reaches the threshold.
        healthy = true;
        await ExecuteAsync(probe);

        // Begin a second outage.
        clock.Advance(TimeSpan.FromSeconds(5));

        healthy = false;
        await ExecuteAsync(probe);

        // The second outage should reach the threshold independently.
        clock.Advance(OutageThreshold);

        var result = await ExecuteAsync(probe);

        result.HasCriticalFailure.Should().BeTrue();

        result.Issues.Should().ContainSingle(issue =>
            issue.Key == "service-health-timeout:Terminology" &&
            issue.Severity == MonitorIssueSeverity.Critical);
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

        bool? healthy = true;
        var probe = CreateProbe(() => healthy, clock);

        // Establish initial healthy state.
        await ExecuteAsync(probe);

        // Start an outage.
        healthy = false;
        await ExecuteAsync(probe);

        clock.Advance(TimeSpan.FromSeconds(100));

        // Health becomes unknown, clearing the outage window.
        healthy = null;
        await ExecuteAsync(probe);

        clock.Advance(TimeSpan.FromSeconds(30));

        // Start a new outage.
        healthy = false;
        await ExecuteAsync(probe);

        clock.Advance(TimeSpan.FromSeconds(100));

        // The new outage has not reached its threshold.
        var result = await ExecuteAsync(probe);

        result.HasCriticalFailure.Should().BeFalse();
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
            [
                new MonitoredService(
                    "Terminology",
                    _ => Task.FromResult<bool?>(true))
            ],
            TimeSpan.Zero,
            OutageThreshold);

        action.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void ZeroOutageThreshold_ThrowsArgumentOutOfRangeException()
    {
        var action = () => new ServiceHealthProbe(
            [
                new MonitoredService(
                    "Terminology",
                    _ => Task.FromResult<bool?>(true))
            ],
            CheckInterval,
            TimeSpan.Zero);

        action.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public async Task ValidationServiceOutage_TriggersCriticalFailureWhileTerminologyIsHealthy()
    {
        var clock = new TestTimeProvider();

        bool? validationHealthy = true;

        var probe = new ServiceHealthProbe(
            [
                new MonitoredService(
                    "Terminology",
                    _ => Task.FromResult<bool?>(true)),

            new MonitoredService(
                "Validation",
                _ => Task.FromResult(validationHealthy))
            ],
            CheckInterval,
            OutageThreshold,
            clock);

        // Both services are initially healthy.
        await ExecuteAsync(probe);

        // Simulate Validation becoming unavailable.
        // Terminology remains healthy.
        validationHealthy = false;

        var initialResult = await ExecuteAsync(probe);

        initialResult.HasCriticalFailure.Should().BeFalse();

        // Advance to the configured outage threshold.
        clock.Advance(OutageThreshold);

        var result = await ExecuteAsync(probe);

        result.HasCriticalFailure.Should().BeTrue();

        result.Issues.Should().ContainSingle(issue =>
            issue.Key == "service-health-timeout:Validation" &&
            issue.Source == "ServiceHealth" &&
            issue.Severity == MonitorIssueSeverity.Critical &&
            issue.Message.Contains("Validation"));
    }

    [Fact]
    public async Task RegenerationPhase_PreservesHealthHistoryAcrossMonitorPhases()
    {
        var clock = new TestTimeProvider();

        bool? healthy = true;
        var probe = CreateProbe(() => healthy, clock);

        // Initial report phase: service is healthy.
        var initialResult = await ExecuteAsync(probe);

        initialResult.HasCriticalFailure.Should().BeFalse();

        // The service becomes unavailable before regeneration.
        healthy = false;

        // Regeneration phase reuses the same probe.
        var regenInitialResult = await ExecuteAsync(probe);

        regenInitialResult.HasCriticalFailure.Should().BeFalse();

        regenInitialResult.Issues.Should().ContainSingle(issue =>
            issue.Key == "service-health-outage:Terminology" &&
            issue.Severity == MonitorIssueSeverity.Warning);

        // The outage persists throughout the configured threshold.
        clock.Advance(OutageThreshold);

        var regenResult = await ExecuteAsync(probe);

        regenResult.HasCriticalFailure.Should().BeTrue();

        regenResult.Issues.Should().ContainSingle(issue =>
            issue.Key == "service-health-timeout:Terminology" &&
            issue.Severity == MonitorIssueSeverity.Critical);
    }

    [Fact]
    public async Task NeverHealthyService_DoesNotStartOutageTimer()
    {
        var clock = new TestTimeProvider();

        var probe = CreateProbe(() => false, clock);

        // Service is unhealthy from the very first check.
        var initialResult = await ExecuteAsync(probe);

        initialResult.HasCriticalFailure.Should().BeFalse();

        // Advance well beyond the configured outage threshold.
        clock.Advance(OutageThreshold + TimeSpan.FromMinutes(5));

        var result = await ExecuteAsync(probe);

        // An outage is not counted until the service
        // has been observed healthy at least once.
        result.HasCriticalFailure.Should().BeFalse();

        result.Issues.Should().NotContain(issue =>
            issue.Key == "service-health-timeout:Terminology");
    }

    private static ServiceHealthProbe CreateProbe(Func<bool?> healthCheck, TimeProvider clock)
    {
        return new ServiceHealthProbe(
            [
                new MonitoredService(
                    "Terminology",
                    _ => Task.FromResult(healthCheck()))
            ],
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
