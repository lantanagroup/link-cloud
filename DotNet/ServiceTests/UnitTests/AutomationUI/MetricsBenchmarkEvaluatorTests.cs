using Automation.UI.Services;
using Automation.UI.Services.Persistence;
using FluentAssertions;

namespace UnitTests.AutomationUI;

[Trait("Category", "UnitTests")]
public class MetricsBenchmarkEvaluatorTests
{
    [Fact]
    public void Target_duration_slo_fails_when_e2e_exceeds_max()
    {
        var doc = Doc(e2e: 120);

        var result = MetricsBenchmarkEvaluator.Evaluate(doc, benchmark: null, targetDurationSeconds: 90, previous: null);

        result.Pass.Should().BeFalse();
        result.Violations.Should().Contain(v => v.Contains("e2eDurationSeconds"));
    }

    [Fact]
    public void Stage_threshold_is_skipped_when_unavailable()
    {
        var doc = Doc(e2e: 10);
        var benchmark = new AutomationMetricsBenchmarkDocument
        {
            Key = "k",
            Thresholds = new Dictionary<string, ThresholdSpec>
            {
                ["stages.validation.p95Ms"] = new() { Max = 100 }
            }
        };

        var result = MetricsBenchmarkEvaluator.Evaluate(doc, benchmark, targetDurationSeconds: null, previous: null);

        result.Pass.Should().BeTrue();
        result.Violations.Should().BeEmpty();
    }

    [Fact]
    public void Stage_p95_threshold_fails_when_available()
    {
        var doc = Doc(e2e: 10);
        doc.Stages["validation"] = new StageLatencySnapshot { Unavailable = false, Count = 4, P95Ms = 5000 };
        var benchmark = new AutomationMetricsBenchmarkDocument
        {
            Key = "k",
            Thresholds = new Dictionary<string, ThresholdSpec>
            {
                ["stages.validation.p95Ms"] = new() { Max = 4000 }
            }
        };

        var result = MetricsBenchmarkEvaluator.Evaluate(doc, benchmark, null, null);

        result.Pass.Should().BeFalse();
        result.Violations.Should().Contain(v => v.Contains("stages.validation.p95Ms"));
    }

    [Fact]
    public void Regression_flags_when_p95_worsens_beyond_percent()
    {
        var previous = Doc(e2e: 60);
        previous.Stages["acquisition"] = new StageLatencySnapshot { Unavailable = false, P95Ms = 1000, Count = 1 };
        var current = Doc(e2e: 60);
        current.Stages["acquisition"] = new StageLatencySnapshot { Unavailable = false, P95Ms = 1300, Count = 1 };
        var benchmark = new AutomationMetricsBenchmarkDocument { Key = "k", RegressionPercent = 10 };

        var result = MetricsBenchmarkEvaluator.Evaluate(current, benchmark, null, previous);

        result.Pass.Should().BeTrue();
        result.PreviousRunId.Should().Be(previous.RunId);
        result.RegressionFlags.Should().Contain(f => f.Contains("stages.acquisition.p95Ms"));
    }

    [Fact]
    public void Patients_per_minute_min_threshold()
    {
        var doc = Doc(e2e: 60);
        doc.Throughput.PatientsPerMinute = 4;
        var benchmark = new AutomationMetricsBenchmarkDocument
        {
            Key = "k",
            Thresholds = new Dictionary<string, ThresholdSpec>
            {
                ["patientsPerMinute"] = new() { Min = 10 }
            }
        };

        var result = MetricsBenchmarkEvaluator.Evaluate(doc, benchmark, null, null);

        result.Pass.Should().BeFalse();
        result.Violations.Should().Contain(v => v.Contains("patientsPerMinute"));
    }

    private static AutomationRunMetricsDocument Doc(double e2e)
    {
        return new AutomationRunMetricsDocument
        {
            RunId = Guid.NewGuid(),
            ScenarioId = Guid.NewGuid(),
            E2eDurationSeconds = e2e,
            Throughput = new ThroughputSnapshot { PatientsPerMinute = 20 },
            Stages = new Dictionary<string, StageLatencySnapshot>(StringComparer.Ordinal)
            {
                ["acquisition"] = new StageLatencySnapshot { Unavailable = true },
                ["validation"] = new StageLatencySnapshot { Unavailable = true }
            }
        };
    }
}
