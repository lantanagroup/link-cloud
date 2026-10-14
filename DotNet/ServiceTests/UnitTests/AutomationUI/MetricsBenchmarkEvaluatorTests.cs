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
        result.Violations.Should().Contain(v => v.Contains("Total run time"));
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
        result.Violations.Should().Contain(v => v.Contains("Validation") && v.Contains("slow time"));
    }

    [Fact]
    public void Regression_flags_when_p95_worsens_beyond_percent()
    {
        var previous = Full(e2e: 60, p95: 1000);
        var current = Full(e2e: 60, p95: 1000);
        current.Stages["acquisition"] = new StageLatencySnapshot { Unavailable = false, P95Ms = 1300, Count = 1 };
        var benchmark = new AutomationMetricsBenchmarkDocument { Key = "k", RegressionPercent = 10 };

        var result = MetricsBenchmarkEvaluator.Evaluate(current, benchmark, null, previous);

        result.Pass.Should().BeTrue();
        result.PreviousRunId.Should().Be(previous.RunId);
        result.RegressionFlags.Should().Contain(f => f.Contains("Data Acquisition") && f.Contains("slower"));
    }

    [Fact]
    public void Partial_snapshot_is_not_a_regression_baseline()
    {
        var previous = Doc(e2e: 40);
        previous.Stages["acquisition"] = new StageLatencySnapshot { Unavailable = false, P95Ms = 100, Count = 1 };
        previous.Stages["normalization"] = new StageLatencySnapshot { Unavailable = false, P95Ms = 100, Count = 1 };
        var current = Full(e2e: 55, p95: 2000);

        var result = MetricsBenchmarkEvaluator.Evaluate(current, benchmark: null, targetDurationSeconds: null, previous);

        result.RegressionFlags.Should().BeEmpty();
        result.PreviousRunId.Should().BeNull();
    }

    [Fact]
    public void SelectBaseline_skips_a_newer_partial_snapshot()
    {
        var partial = Doc(e2e: 40);
        partial.Stages["acquisition"] = new StageLatencySnapshot { Unavailable = false, P95Ms = 100, Count = 1 };
        var full = Full(e2e: 60, p95: 1000);

        var chosen = MetricsBenchmarkEvaluator.SelectBaseline([partial, full]);

        chosen.Should().BeSameAs(full);
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
        result.Violations.Should().Contain(v => v.Contains("Patients per minute"));
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

    private static AutomationRunMetricsDocument Full(double e2e, double p95)
    {
        var document = Doc(e2e);
        foreach (var stage in new[] { "acquisition", "normalization", "measureeval", "validation", "submission" })
            document.Stages[stage] = new StageLatencySnapshot { Unavailable = false, P95Ms = p95, Count = 1 };
        return document;
    }
}
