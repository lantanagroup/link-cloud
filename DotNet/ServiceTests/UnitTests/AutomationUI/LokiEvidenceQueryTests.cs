using Automation.UI.Services;
using FluentAssertions;
using LantanaGroup.Automation.Helpers;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.AutomationUI;

[Trait("Category", "UnitTests")]
public class LokiEvidenceQueryTests
{
    [Fact]
    public void Lookback_widens_to_at_least_configured_and_step_windows()
    {
        LokiEvidenceQuery.LookbackForAttempt(TimeSpan.FromMinutes(5), 0).Should().Be(TimeSpan.FromMinutes(5));
        LokiEvidenceQuery.LookbackForAttempt(TimeSpan.FromMinutes(5), 1).Should().Be(TimeSpan.FromMinutes(10));
        LokiEvidenceQuery.LookbackForAttempt(TimeSpan.FromMinutes(5), 2).Should().Be(TimeSpan.FromMinutes(15));
        LokiEvidenceQuery.LookbackForAttempt(TimeSpan.FromMinutes(5), 3).Should().Be(TimeSpan.FromMinutes(20));
        LokiEvidenceQuery.LookbackForAttempt(TimeSpan.FromMinutes(30), 1).Should().Be(TimeSpan.FromMinutes(30));
    }

    [Fact]
    public void Delay_before_retry_attempts_is_5_10_20_seconds()
    {
        LokiEvidenceQuery.DelayBeforeAttempt(0).Should().BeNull();
        LokiEvidenceQuery.DelayBeforeAttempt(1).Should().Be(TimeSpan.FromSeconds(5));
        LokiEvidenceQuery.DelayBeforeAttempt(2).Should().Be(TimeSpan.FromSeconds(10));
        LokiEvidenceQuery.DelayBeforeAttempt(3).Should().Be(TimeSpan.FromSeconds(20));
    }

    [Fact]
    public void Empty_logs_need_retry_when_suite_requires_evidence()
    {
        LokiEvidenceQuery.NeedsRetry(["Observation"], ["Observation"], []).Should().BeTrue();
        LokiEvidenceQuery.NeedsRetry([], ["Observation"], []).Should().BeFalse();
    }

    [Fact]
    public void Missing_acquired_type_needs_retry()
    {
        var logs = new List<string>
        {
            SummaryLine("Encounter", "enc-1")
        };

        LokiEvidenceQuery.NeedsRetry(
            ["Encounter", "Observation", "Patient"],
            ["Encounter", "Observation"],
            logs).Should().BeTrue();
    }

    [Fact]
    public void Missing_patient_evidence_does_not_retry_when_patient_is_not_acquired()
    {
        var logs = new List<string>
        {
            SummaryLine("Observation", "obs-1")
        };

        LokiEvidenceQuery.NeedsRetry(
            ["Observation", "Patient"],
            ["Observation"],
            logs).Should().BeFalse();
    }

    [Fact]
    public void Acquired_types_present_do_not_retry()
    {
        var logs = new List<string>
        {
            SummaryLine("Observation", "obs-1"),
            SummaryLine("Encounter", "enc-1")
        };

        LokiEvidenceQuery.NeedsRetry(
            ["Observation", "Encounter", "Patient"],
            ["Observation", "Encounter"],
            logs).Should().BeFalse();
    }

    [Fact]
    public async Task CollectWithRetry_requeries_until_acquired_types_are_present()
    {
        var attempts = 0;
        var delays = new List<TimeSpan>();
        var output = new CapturingOutput();

        var logs = await LokiEvidenceQuery.CollectWithRetryAsync(
            TimeSpan.FromMinutes(5),
            ["Observation", "Patient"],
            ["Observation"],
            (lookback, _) =>
            {
                attempts++;
                lookback.Should().Be(attempts == 1 ? TimeSpan.FromMinutes(5) : TimeSpan.FromMinutes(10));
                if (attempts == 1)
                    return Task.FromResult(new List<string>());

                return Task.FromResult(new List<string> { SummaryLine("Observation", "obs-1") });
            },
            (delay, _) =>
            {
                delays.Add(delay);
                return Task.CompletedTask;
            },
            output);

        attempts.Should().Be(2);
        delays.Should().Equal(TimeSpan.FromSeconds(5));
        logs.Should().ContainSingle();
        output.Lines.Should().Contain(l => l.Contains("Loki evidence incomplete", StringComparison.Ordinal));
    }

    [Fact]
    public async Task CollectWithRetry_stops_after_max_attempts_when_still_empty()
    {
        var attempts = 0;
        var output = new CapturingOutput();

        var logs = await LokiEvidenceQuery.CollectWithRetryAsync(
            TimeSpan.FromMinutes(5),
            ["Observation"],
            ["Observation"],
            (_, _) =>
            {
                attempts++;
                return Task.FromResult(new List<string>());
            },
            (_, _) => Task.CompletedTask,
            output);

        attempts.Should().Be(LokiEvidenceQuery.MaxAttempts);
        logs.Should().BeEmpty();
        output.Lines.Should().Contain(l => l.Contains("still incomplete", StringComparison.Ordinal));
    }

    private static string SummaryLine(string resourceType, string resourceId) =>
        $"[NormalizationExecutionSummary] FacilityId=f1, CorrelationId=c1, PatientId=p1, ResourceType={resourceType}, ResourceId={resourceId}, Steps=[1:RemoveExtensions:Remove Common Extensions:Success]";

    private sealed class CapturingOutput : IAutomationOutput
    {
        public List<string> Lines { get; } = [];
        public void WriteLine(string message) => Lines.Add(message);
        public void WriteLine(string format, params object[] args) => Lines.Add(string.Format(format, args));
    }
}
