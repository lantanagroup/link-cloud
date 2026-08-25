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
                violations.Add($"{key} {Format(value.Value)} exceeds max {Format(max)}");
            if (spec.Min is double min && value.Value < min)
                violations.Add($"{key} {Format(value.Value)} is below min {Format(min)}");
        }

        var flags = new List<string>();
        var percent = benchmark?.RegressionPercent > 0 ? benchmark.RegressionPercent : 10;
        if (previous != null)
        {
            if (previous.E2eDurationSeconds > 0)
            {
                var limit = previous.E2eDurationSeconds * (1 + percent / 100.0);
                if (document.E2eDurationSeconds > limit)
                    flags.Add($"e2eDurationSeconds {Format(document.E2eDurationSeconds)} is >{percent}% worse than previous {Format(previous.E2eDurationSeconds)}");
            }

            foreach (var stage in document.Stages.Keys)
            {
                if (!document.Stages.TryGetValue(stage, out var current) || current.Unavailable)
                    continue;
                if (!previous.Stages.TryGetValue(stage, out var prior) || prior.Unavailable || prior.P95Ms <= 0)
                    continue;

                var limit = prior.P95Ms * (1 + percent / 100.0);
                if (current.P95Ms > limit)
                    flags.Add($"stages.{stage}.p95Ms {Format(current.P95Ms)} is >{percent}% worse than previous {Format(prior.P95Ms)}");
            }
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

    private static ThresholdSpec Merge(ThresholdSpec left, ThresholdSpec right) => new()
    {
        Min = left.Min is double lmin && right.Min is double rmin ? Math.Max(lmin, rmin) : left.Min ?? right.Min,
        Max = left.Max is double lmax && right.Max is double rmax ? Math.Min(lmax, rmax) : left.Max ?? right.Max
    };

    private static string Format(double value) => value.ToString("0.###");
}
