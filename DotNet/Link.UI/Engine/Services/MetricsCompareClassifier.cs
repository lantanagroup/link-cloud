using System.Globalization;

namespace Automation.UI.Services;

public enum MetricsChangeKind
{
    Unclassified,
    Unchanged,
    Improvement,
    Degradation
}

public enum MetricsDeltaWording
{
    Duration,
    Amount,
    Throughput
}

public readonly record struct MetricsChange(MetricsChangeKind Kind, string Text, string CssClass)
{
    public string Token => Kind switch
    {
        MetricsChangeKind.Improvement => "improvement",
        MetricsChangeKind.Degradation => "degradation",
        MetricsChangeKind.Unchanged => "unchanged",
        _ => "unclassified"
    };

    public static MetricsChange Unclassified => new(MetricsChangeKind.Unclassified, "", "");

    public static MetricsChange Unchanged(string text) =>
        new(MetricsChangeKind.Unchanged, text, "text-muted");

    public static MetricsChange Improvement(string text) =>
        new(MetricsChangeKind.Improvement, text, "text-success");

    public static MetricsChange Degradation(string text) =>
        new(MetricsChangeKind.Degradation, text, "text-danger");
}

/// <summary>
/// Classifies one compare-row delta. Under 1% is "about the same". From 1% through 5%
/// stays muted. Only a change past 5%, or a Passed/Failed flip, is an improvement or
/// a degradation. A missing, non-positive, or non-finite value stays unclassified.
/// Percent labels keep one decimal when the tenths digit is not zero.
/// </summary>
public static class MetricsCompareClassifier
{
    // Ratio, not percent: 0.05 is the existing "past 5%" color cutoff (strictly greater).
    private const double ColoredRatio = 0.05d;
    private const double AboutTheSamePercent = 1d;

    public static string DisplayResult(string? outcome) => outcome switch
    {
        "Succeeded" => "Passed",
        null => "",
        _ => outcome
    };

    public static MetricsChange Classify(
        double? baseline,
        double? later,
        bool higherIsBetter,
        MetricsDeltaWording wording)
    {
        if (baseline is not double baseValue
            || later is not double laterValue
            || baseValue <= 0d
            || !double.IsFinite(baseValue)
            || !double.IsFinite(laterValue))
            return MetricsChange.Unclassified;

        var ratio = (laterValue - baseValue) / baseValue;
        var percent = ratio * 100d;
        if (Math.Abs(percent) < AboutTheSamePercent)
            return MetricsChange.Unchanged("about the same");

        var amount = FormatAmount(percent);
        var increased = ratio > 0d;
        var text = wording switch
        {
            MetricsDeltaWording.Duration => increased ? $"{amount}% slower" : $"{amount}% faster",
            MetricsDeltaWording.Throughput => increased
                ? $"{amount}% more throughput"
                : $"{amount}% less throughput",
            _ => increased ? $"{amount}% more" : $"{amount}% less"
        };

        if (Math.Abs(ratio) <= ColoredRatio)
            return MetricsChange.Unchanged(text);

        var improved = higherIsBetter ? increased : !increased;
        return improved ? MetricsChange.Improvement(text) : MetricsChange.Degradation(text);
    }

    public static MetricsChange ClassifyResult(string? baselineOutcome, string? laterOutcome)
    {
        var baseline = DisplayResult(string.IsNullOrWhiteSpace(baselineOutcome) ? null : baselineOutcome);
        var later = DisplayResult(string.IsNullOrWhiteSpace(laterOutcome) ? null : laterOutcome);
        if (baseline.Length == 0 && later.Length == 0)
            return MetricsChange.Unclassified;

        if (string.Equals(baseline, later, StringComparison.Ordinal))
            return MetricsChange.Unchanged("unchanged");

        if (baseline == "Failed" && later == "Passed")
            return MetricsChange.Improvement("Failed → Passed");

        if (baseline == "Passed" && later == "Failed")
            return MetricsChange.Degradation("Passed → Failed");

        return MetricsChange.Unclassified;
    }

    public static string NormalizeFilter(string? filter)
    {
        if (string.Equals(filter, "improvement", StringComparison.OrdinalIgnoreCase))
            return "improvement";
        if (string.Equals(filter, "degradation", StringComparison.OrdinalIgnoreCase))
            return "degradation";
        return "all";
    }

    public static bool MatchesFilter(MetricsChangeKind kind, string? filter)
    {
        var normalized = NormalizeFilter(filter);
        return normalized switch
        {
            "improvement" => kind == MetricsChangeKind.Improvement,
            "degradation" => kind == MetricsChangeKind.Degradation,
            _ => true
        };
    }

    // One decimal, and a whole number stays "5%" rather than "5.0%".
    private static string FormatAmount(double percent)
    {
        var tenths = (long)Math.Round(Math.Abs(percent) * 10d, MidpointRounding.AwayFromZero);
        var whole = tenths / 10;
        var fraction = tenths % 10;
        return fraction == 0
            ? whole.ToString(CultureInfo.InvariantCulture)
            : string.Create(CultureInfo.InvariantCulture, $"{whole}.{fraction}");
    }
}
