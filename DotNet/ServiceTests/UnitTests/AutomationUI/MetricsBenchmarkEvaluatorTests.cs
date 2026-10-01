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
        var previous = Doc(e2e: 60);
        previous.Stages["acquisition"] = new StageLatencySnapshot { Unavailable = false, P95Ms = 1000, Count = 1 };
        var current = Doc(e2e: 60);
        current.Stages["acquisition"] = new StageLatencySnapshot { Unavailable = false, P95Ms = 1300, Count = 1 };
        var benchmark = new AutomationMetricsBenchmarkDocument { Key = "k", RegressionPercent = 10 };

        var result = MetricsBenchmarkEvaluator.Evaluate(current, benchmark, null, previous);

        result.Pass.Should().BeTrue();
        result.PreviousRunId.Should().Be(previous.RunId);
        result.RegressionFlags.Should().Contain(f => f.Contains("Data Acquisition") && f.Contains("slower"));
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

    [Fact]
    public void A_drop_in_cache_hit_ratio_is_flagged()
    {
        var previous = Doc(100);
        previous.ResourceCache = Cache(hitRatio: 0.90);
        var current = Doc(100);
        current.ResourceCache = Cache(hitRatio: 0.50);

        var result = MetricsBenchmarkEvaluator.Evaluate(current, null, null, previous);

        Assert.Contains(result.RegressionFlags, f => f.Contains("hit ratio", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void A_rise_in_barrier_wait_is_flagged()
    {
        var previous = Doc(100);
        previous.ResourceCache = Cache(drainP95: 10);
        var current = Doc(100);
        current.ResourceCache = Cache(drainP95: 100);

        var result = MetricsBenchmarkEvaluator.Evaluate(current, null, null, previous);

        Assert.Contains(result.RegressionFlags, f => f.Contains("barrier", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void A_barrier_appearing_for_the_first_time_is_not_flagged()
    {
        // The first Hybrid run after an ABS baseline: the baseline had no barrier at all, so
        // comparing against zero would report the design working as a regression.
        var previous = Doc(100);
        previous.ResourceCache = Cache(drainP95: 0);
        var current = Doc(100);
        current.ResourceCache = Cache(drainP95: 250);

        var result = MetricsBenchmarkEvaluator.Evaluate(current, null, null, previous);

        Assert.DoesNotContain(result.RegressionFlags, f => f.Contains("barrier", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void An_unavailable_cache_snapshot_is_not_compared()
    {
        var previous = Doc(100);
        previous.ResourceCache = Cache(hitRatio: 0.90);
        var current = Doc(100);
        current.ResourceCache = new ResourceCacheSnapshot { Unavailable = true };

        var result = MetricsBenchmarkEvaluator.Evaluate(current, null, null, previous);

        Assert.DoesNotContain(result.RegressionFlags, f => f.Contains("cache", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void A_write_that_exhausted_its_retries_is_a_violation_not_a_slowdown()
    {
        var previous = Doc(100);
        previous.ResourceCache = Cache();
        var current = Doc(100);
        current.ResourceCache = Cache(exhausted: 3);

        var result = MetricsBenchmarkEvaluator.Evaluate(current, null, null, previous);

        // The UI files every regression flag under "Slower than last successful run", and a
        // durability failure listed there reads as a performance nitpick.
        Assert.Contains(result.Violations, v => v.Contains("exhausted their retries", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(result.RegressionFlags, f => f.Contains("exhausted", StringComparison.OrdinalIgnoreCase));
        Assert.False(result.Pass);
    }

    [Fact]
    public void A_write_that_exhausted_its_retries_is_reported_without_a_previous_run()
    {
        var current = Doc(100);
        current.ResourceCache = Cache(exhausted: 2);

        // The drift comparisons need a previous run; this does not, and a first run is exactly
        // when a durability failure must not go unreported.
        var result = MetricsBenchmarkEvaluator.Evaluate(current, null, null, null);

        Assert.Contains(result.Violations, v => v.Contains("exhausted their retries", StringComparison.OrdinalIgnoreCase));
    }

    private static ResourceCacheSnapshot Cache(
        double hitRatio = 0.8,
        double drainP95 = 5,
        double readP95 = 5,
        double exhausted = 0) => new()
    {
        Unavailable = false,
        HitCount = 80,
        FallbackCount = 20,
        HitRatio = hitRatio,
        ReadP95Ms = readP95,
        DrainWaitP95Ms = drainP95,
        WriteExhaustedCount = exhausted
    };
}
