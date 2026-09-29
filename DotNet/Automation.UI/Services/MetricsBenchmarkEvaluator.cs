using Automation.UI.Services.Persistence;

namespace Automation.UI.Services;

public sealed record BenchmarkEvaluation(
    string? Key,
    bool Pass,
    IReadOnlyList<string> Violations,
    Guid? PreviousRunId,
    IReadOnlyList<string> RegressionFlags);

public static class MetricsBenchmarkEvaluator
{
    public static BenchmarkEvaluation Evaluate(
        AutomationRunMetricsDocument document,
        AutomationMetricsBenchmarkDocument? benchmark,
        int? targetDurationSeconds,
        AutomationRunMetricsDocument? previous)
    {
        var thresholds = new Dictionary<string, ThresholdSpec>(StringComparer.Ordinal);
        if (targetDurationSeconds is > 0)
            thresholds["e2eDurationSeconds"] = new ThresholdSpec { Max = targetDurationSeconds.Value };

        if (benchmark?.Thresholds != null)
        {
            foreach (var (key, spec) in benchmark.Thresholds)
            {
                if (thresholds.TryGetValue(key, out var existing))
                    thresholds[key] = Merge(existing, spec);
                else
                    thresholds[key] = spec;
            }
        }

        var violations = new List<string>();
        foreach (var (key, spec) in thresholds)
        {
            var value = TryGetValue(document, key);
            if (value is null)
                continue;

            if (spec.Max is double max && value.Value > max)
                violations.Add($"{FriendlyName(key)} was {Format(value.Value)}; the limit is {Format(max)}.");
            if (spec.Min is double min && value.Value < min)
                violations.Add($"{FriendlyName(key)} was {Format(value.Value)}; it needs to be at least {Format(min)}.");
        }

        var flags = new List<string>();
        var percent = benchmark?.RegressionPercent > 0 ? benchmark.RegressionPercent : 10;
        if (previous != null)
        {
            if (previous.E2eDurationSeconds > 0)
            {
                var limit = previous.E2eDurationSeconds * (1 + percent / 100.0);
                if (document.E2eDurationSeconds > limit)
                    flags.Add($"Total run time ({Format(document.E2eDurationSeconds)} sec) was more than {percent}% slower than the last successful run ({Format(previous.E2eDurationSeconds)} sec).");
            }

            foreach (var stage in document.Stages.Keys)
            {
                if (!document.Stages.TryGetValue(stage, out var current) || current.Unavailable)
                    continue;
                if (!previous.Stages.TryGetValue(stage, out var prior) || prior.Unavailable || prior.P95Ms <= 0)
                    continue;

                var limit = prior.P95Ms * (1 + percent / 100.0);
                if (current.P95Ms > limit)
                    flags.Add($"{FriendlyStage(stage)} slow time ({Format(current.P95Ms)} ms) was more than {percent}% slower than the last successful run ({Format(prior.P95Ms)} ms).");
            }

            AddResourceCacheFlags(document, previous, percent, flags);
        }

        return new BenchmarkEvaluation(
            benchmark?.Key ?? document.BenchmarkKey,
            violations.Count == 0,
            violations,
            previous?.RunId,
            flags);
    }

    internal static double? TryGetValue(AutomationRunMetricsDocument document, string path)
    {
        if (string.Equals(path, "e2eDurationSeconds", StringComparison.Ordinal))
            return document.E2eDurationSeconds;
        if (string.Equals(path, "patientsPerMinute", StringComparison.Ordinal))
            return document.Throughput.PatientsPerMinute;
        if (string.Equals(path, "resourcesPerSecond", StringComparison.Ordinal))
            return document.Throughput.ResourcesPerSecond;
        if (string.Equals(path, "errorRate", StringComparison.Ordinal))
        {
            var count = document.Stages.Values.Where(s => !s.Unavailable).Sum(s => s.Count);
            if (count <= 0)
                return null;
            var errors = document.Stages.Values.Where(s => !s.Unavailable).Sum(s => s.ErrorCount);
            return errors / count;
        }

        const string stagesPrefix = "stages.";
        if (!path.StartsWith(stagesPrefix, StringComparison.Ordinal))
            return null;

        var rest = path[stagesPrefix.Length..];
        var dot = rest.LastIndexOf('.');
        if (dot <= 0)
            return null;

        var stage = rest[..dot];
        var field = rest[(dot + 1)..];
        if (!document.Stages.TryGetValue(stage, out var snapshot) || snapshot.Unavailable)
            return null;

        return field switch
        {
            "p50Ms" => snapshot.P50Ms,
            "p95Ms" => snapshot.P95Ms,
            "p99Ms" => snapshot.P99Ms,
            "count" => snapshot.Count,
            "errorCount" => snapshot.ErrorCount,
            _ => null
        };
    }

    internal static string FriendlyName(string path) => path switch
    {
        "e2eDurationSeconds" => "Total run time (seconds)",
        "patientsPerMinute" => "Patients per minute",
        "resourcesPerSecond" => "Resources per second",
        "errorRate" => "Error rate",
        _ when path.StartsWith("stages.", StringComparison.Ordinal) => FriendlyStagePath(path),
        _ => path
    };

    internal static string FriendlyStage(string key) => key switch
    {
        "acquisition" => "Data Acquisition",
        "normalization" => "Normalization",
        "measureeval" => "Measure Evaluation",
        "validation" => "Validation",
        "submission" => "Submission",
        _ => key
    };

    private static string FriendlyStagePath(string path)
    {
        var rest = path["stages.".Length..];
        var dot = rest.LastIndexOf('.');
        var stage = dot > 0 ? rest[..dot] : rest;
        var field = dot > 0 ? rest[(dot + 1)..] : "";
        var fieldLabel = field switch
        {
            "p50Ms" => "typical time (ms)",
            "p95Ms" => "slow time (ms)",
            "p99Ms" => "slowest time (ms)",
            "count" => "operation count",
            "errorCount" => "error count",
            _ => field
        };
        return $"{FriendlyStage(stage)} {fieldLabel}";
    }

    private static ThresholdSpec Merge(ThresholdSpec left, ThresholdSpec right) => new()
    {
        Min = left.Min is double lmin && right.Min is double rmin ? Math.Max(lmin, rmin) : left.Min ?? right.Min,
        Max = left.Max is double lmax && right.Max is double rmax ? Math.Min(lmax, rmax) : left.Max ?? right.Max
    };

    private static string Format(double value) => value.ToString("0.###");

    /// <remarks>
    /// Each comparison is skipped when the previous run has nothing to compare against, mirroring
    /// the stage guard. That matters most on the first Hybrid run after an ABS baseline: the
    /// baseline has no cache reads and no barrier at all, and flagging a drain wait that went from
    /// nothing to something would be reporting the design working as a regression.
    /// </remarks>
    private static void AddResourceCacheFlags(
        AutomationRunMetricsDocument document,
        AutomationRunMetricsDocument previous,
        double percent,
        List<string> flags)
    {
        var current = document.ResourceCache;
        var prior = previous.ResourceCache;

        if (current is null || prior is null || current.Unavailable || prior.Unavailable)
            return;

        // Hit ratio is the one metric here where lower is worse.
        if (prior.HitRatio > 0)
        {
            var floor = prior.HitRatio * (1 - percent / 100.0);
            if (current.HitRatio < floor)
                flags.Add($"Resource cache hit ratio ({Format(current.HitRatio * 100)}%) was more than {percent}% below the last successful run ({Format(prior.HitRatio * 100)}%).");
        }

        if (prior.DrainWaitP95Ms > 0)
        {
            var limit = prior.DrainWaitP95Ms * (1 + percent / 100.0);
            if (current.DrainWaitP95Ms > limit)
                flags.Add($"Durability barrier slow time ({Format(current.DrainWaitP95Ms)} ms) was more than {percent}% slower than the last successful run ({Format(prior.DrainWaitP95Ms)} ms).");
        }

        if (prior.ReadP95Ms > 0)
        {
            var limit = prior.ReadP95Ms * (1 + percent / 100.0);
            if (current.ReadP95Ms > limit)
                flags.Add($"Resource cache read slow time ({Format(current.ReadP95Ms)} ms) was more than {percent}% slower than the last successful run ({Format(prior.ReadP95Ms)} ms).");
        }

        // Not a drift comparison: a write that exhausted its retries left a cache key with no
        // durable copy behind it, which is worth surfacing on its own.
        if (current.WriteExhaustedCount > 0)
            flags.Add($"{Format(current.WriteExhaustedCount)} durable cache write(s) exhausted their retries; those correlations have no durable copy.");
    }
}
