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
        // A snapshot that missed pipeline steps is not a baseline. Its recorded steps ran against
        // an unfinished report, so a later full run looks slower even when the pipeline did not.
        var baseline = previous != null && IsFullBaseline(previous) ? previous : null;
        if (baseline != null)
        {
            if (baseline.E2eDurationSeconds > 0)
            {
                var limit = baseline.E2eDurationSeconds * (1 + percent / 100.0);
                if (document.E2eDurationSeconds > limit)
                    flags.Add($"Total run time ({Format(document.E2eDurationSeconds)} sec) was more than {percent}% slower than the last successful run ({Format(baseline.E2eDurationSeconds)} sec).");
            }

            foreach (var stage in document.Stages.Keys)
            {
                if (!document.Stages.TryGetValue(stage, out var current) || current.Unavailable)
                    continue;
                if (!baseline.Stages.TryGetValue(stage, out var prior) || prior.Unavailable || prior.P95Ms <= 0)
                    continue;

                if (StageBeyondResolution(prior.P95Ms, current.P95Ms, percent))
                    flags.Add($"{FriendlyStage(stage)} slow time ({Format(current.P95Ms)} ms) was more than {percent}% slower than the last successful run ({Format(prior.P95Ms)} ms).");
            }
        }

        return new BenchmarkEvaluation(
            benchmark?.Key ?? document.BenchmarkKey,
            violations.Count == 0,
            violations,
            baseline?.RunId,
            flags);
    }

    /// <summary>How many recent succeeded snapshots to read when the newest one is missing pipeline steps.</summary>
    public const int BaselineLookback = 16;

    /// <summary>
    /// Upper bounds of the stage-duration histograms, in milliseconds.
    /// Same boundaries as the Java duration buckets. A quantile inside a bucket is an
    /// estimate, so two runs in the same bucket or in neighboring buckets are not comparable.
    /// </summary>
    internal static readonly double[] DurationBucketsMs =
    [
        1, 2, 5, 10, 25, 50, 100, 250, 500, 1000, 2500, 5000, 10000, 15000, 30000, 45000, 60000
    ];

    /// <summary>
    /// True when the current slow time is beyond the previous bucket even after the regression margin.
    /// A move into the next bucket only is inside the histogram's resolution.
    /// </summary>
    internal static bool StageBeyondResolution(double priorMs, double currentMs, double percent)
    {
        if (priorMs <= 0 || currentMs <= priorMs * (1 + percent / 100.0))
            return false;

        var margin = 1 + percent / 100.0;
        return BucketLower(currentMs) > BucketUpper(priorMs) * margin;
    }

    private static double BucketUpper(double ms)
    {
        foreach (var bound in DurationBucketsMs)
        {
            if (ms <= bound)
                return bound;
        }

        return double.PositiveInfinity;
    }

    private static double BucketLower(double ms)
    {
        var lower = 0d;
        foreach (var bound in DurationBucketsMs)
        {
            if (ms <= bound)
                return lower;
            lower = bound;
        }

        return DurationBucketsMs[^1];
    }

    /// <summary>
    /// Every pipeline step recorded a slow time. End-to-end time alone is not enough:
    /// a run that stopped early still has a duration.
    /// </summary>
    internal static bool IsFullBaseline(AutomationRunMetricsDocument document)
    {
        if (document.E2eDurationSeconds <= 0)
            return false;

        foreach (var stage in RunMetricsSnapshotService.StageHistograms)
        {
            if (!document.Stages.TryGetValue(stage.Stage, out var snapshot) || snapshot.Unavailable || snapshot.P95Ms <= 0)
                return false;
        }

        return true;
    }

    /// <summary>Newest succeeded snapshot in the list that recorded every pipeline step.</summary>
    internal static AutomationRunMetricsDocument? SelectBaseline(IEnumerable<AutomationRunMetricsDocument>? newestFirst)
    {
        if (newestFirst is null)
            return null;

        foreach (var document in newestFirst)
        {
            if (IsFullBaseline(document))
                return document;
        }

        return null;
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
}
