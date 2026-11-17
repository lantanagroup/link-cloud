using Automation.UI.Models.Metrics;
using Automation.UI.Services;
using FluentAssertions;

namespace UnitTests.AutomationUI;

[Trait("Category", "UnitTests")]
public class MetricsCompareClassifierTests
{
    [Theory]
    [InlineData(100, 90, MetricsDeltaWording.Duration, "10% faster")]
    [InlineData(100, 80, MetricsDeltaWording.Amount, "20% less")]
    public void Lower_is_better_colors_a_drop_as_an_improvement(
        double baseline,
        double later,
        MetricsDeltaWording wording,
        string text)
    {
        var change = MetricsCompareClassifier.Classify(baseline, later, higherIsBetter: false, wording);

        change.Kind.Should().Be(MetricsChangeKind.Improvement);
        change.CssClass.Should().Be("text-success");
        change.Text.Should().Be(text);
        change.Token.Should().Be("improvement");
    }

    [Fact]
    public void Lower_is_better_colors_a_rise_as_a_degradation()
    {
        var change = MetricsCompareClassifier.Classify(100, 112, higherIsBetter: false, MetricsDeltaWording.Duration);

        change.Kind.Should().Be(MetricsChangeKind.Degradation);
        change.CssClass.Should().Be("text-danger");
        change.Text.Should().Be("12% slower");
    }

    [Fact]
    public void Higher_is_better_colors_more_throughput_as_an_improvement()
    {
        var change = MetricsCompareClassifier.Classify(10, 12, higherIsBetter: true, MetricsDeltaWording.Throughput);

        change.Kind.Should().Be(MetricsChangeKind.Improvement);
        change.CssClass.Should().Be("text-success");
        change.Text.Should().Be("20% more throughput");
    }

    [Fact]
    public void Higher_is_better_colors_less_throughput_as_a_degradation()
    {
        var change = MetricsCompareClassifier.Classify(10, 8, higherIsBetter: true, MetricsDeltaWording.Throughput);

        change.Kind.Should().Be(MetricsChangeKind.Degradation);
        change.CssClass.Should().Be("text-danger");
        change.Text.Should().Be("20% less throughput");
    }

    [Theory]
    [InlineData(100, 100.4)]
    [InlineData(100, 99.6)]
    public void Under_one_percent_is_about_the_same(double baseline, double later)
    {
        var change = MetricsCompareClassifier.Classify(baseline, later, higherIsBetter: false, MetricsDeltaWording.Duration);

        change.Kind.Should().Be(MetricsChangeKind.Unchanged);
        change.CssClass.Should().Be("text-muted");
        change.Text.Should().Be("about the same");
    }

    [Theory]
    [InlineData(100, 101, "1% slower")]
    [InlineData(100, 103, "3% slower")]
    [InlineData(100, 105, "5% slower")]
    [InlineData(100, 97, "3% faster")]
    public void One_through_five_percent_stays_muted_and_off_the_filters(double baseline, double later, string text)
    {
        var change = MetricsCompareClassifier.Classify(baseline, later, higherIsBetter: false, MetricsDeltaWording.Duration);

        change.Kind.Should().Be(MetricsChangeKind.Unchanged);
        change.CssClass.Should().Be("text-muted");
        change.Text.Should().Be(text);
        MetricsCompareClassifier.MatchesFilter(change.Kind, "improvement").Should().BeFalse();
        MetricsCompareClassifier.MatchesFilter(change.Kind, "degradation").Should().BeFalse();
        MetricsCompareClassifier.MatchesFilter(change.Kind, "all").Should().BeTrue();
    }

    [Fact]
    public void Just_over_five_percent_keeps_one_decimal_and_is_colored()
    {
        var change = MetricsCompareClassifier.Classify(100, 105.4, higherIsBetter: false, MetricsDeltaWording.Amount);

        change.Kind.Should().Be(MetricsChangeKind.Degradation);
        change.Text.Should().Be("5.4% more");
        MetricsCompareClassifier.MatchesFilter(change.Kind, "degradation").Should().BeTrue();
        MetricsCompareClassifier.MatchesFilter(change.Kind, "improvement").Should().BeFalse();
    }

    [Theory]
    [InlineData("Failed", "Succeeded", MetricsChangeKind.Improvement, "text-success", "Failed → Passed")]
    [InlineData("Succeeded", "Failed", MetricsChangeKind.Degradation, "text-danger", "Passed → Failed")]
    [InlineData("Succeeded", "Succeeded", MetricsChangeKind.Unchanged, "text-muted", "unchanged")]
    [InlineData("Failed", "Failed", MetricsChangeKind.Unchanged, "text-muted", "unchanged")]
    [InlineData("Passed", "Passed", MetricsChangeKind.Unchanged, "text-muted", "unchanged")]
    public void Result_flip_follows_passed_and_failed(
        string baseline,
        string later,
        MetricsChangeKind kind,
        string cssClass,
        string text)
    {
        var change = MetricsCompareClassifier.ClassifyResult(baseline, later);

        change.Kind.Should().Be(kind);
        change.CssClass.Should().Be(cssClass);
        change.Text.Should().Be(text);
    }

    [Fact]
    public void Result_change_that_is_not_a_pass_fail_flip_stays_unclassified()
    {
        var change = MetricsCompareClassifier.ClassifyResult("Succeeded", "Cancelled");

        change.Kind.Should().Be(MetricsChangeKind.Unclassified);
        change.Text.Should().BeEmpty();
        change.CssClass.Should().BeEmpty();
    }

    public static IEnumerable<object?[]> MissingOrZeroBaselines()
    {
        yield return [null, 100d];
        yield return [0d, 100d];
        yield return [100d, null];
    }

    [Theory]
    [MemberData(nameof(MissingOrZeroBaselines))]
    public void Missing_or_zero_baseline_and_a_missing_later_value_stay_unclassified(double? baseline, double? later)
    {
        var change = MetricsCompareClassifier.Classify(baseline, later, higherIsBetter: false, MetricsDeltaWording.Duration);

        change.Kind.Should().Be(MetricsChangeKind.Unclassified);
        change.Token.Should().Be("unclassified");
        MetricsCompareClassifier.MatchesFilter(change.Kind, "improvement").Should().BeFalse();
        MetricsCompareClassifier.MatchesFilter(change.Kind, "degradation").Should().BeFalse();
        MetricsCompareClassifier.MatchesFilter(change.Kind, "all").Should().BeTrue();
        MetricsCompareClassifier.MatchesFilter(change.Kind, "nope").Should().BeTrue();
    }

    [Fact]
    public void A_zero_later_value_with_a_real_baseline_is_classified()
    {
        var change = MetricsCompareClassifier.Classify(80, 0, higherIsBetter: false, MetricsDeltaWording.Duration);

        change.Kind.Should().Be(MetricsChangeKind.Improvement);
        change.Text.Should().Be("100% faster");
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("", "")]
    [InlineData("   ", "")]
    [InlineData(null, "   ")]
    public void Result_with_no_outcome_on_either_run_stays_blank(string? baseline, string? later)
    {
        var change = MetricsCompareClassifier.ClassifyResult(baseline, later);

        change.Kind.Should().Be(MetricsChangeKind.Unclassified);
        change.Text.Should().BeEmpty();
        change.CssClass.Should().BeEmpty();
        change.Token.Should().Be("unclassified");
    }

    public static IEnumerable<object[]> NonFiniteOrNonPositiveValues()
    {
        yield return [double.NaN, 100d];
        yield return [100d, double.NaN];
        yield return [double.PositiveInfinity, 100d];
        yield return [100d, double.PositiveInfinity];
        yield return [double.NegativeInfinity, 100d];
        yield return [100d, double.NegativeInfinity];
        yield return [-1d, 100d];
        yield return [-0.5d, 10d];
    }

    [Theory]
    [MemberData(nameof(NonFiniteOrNonPositiveValues))]
    public void Non_finite_or_non_positive_baseline_stays_unclassified(double baseline, double later)
    {
        var change = MetricsCompareClassifier.Classify(baseline, later, higherIsBetter: false, MetricsDeltaWording.Duration);

        change.Kind.Should().Be(MetricsChangeKind.Unclassified);
        change.Text.Should().BeEmpty();
        change.Token.Should().Be("unclassified");
        MetricsCompareClassifier.MatchesFilter(change.Kind, "all").Should().BeTrue();
        MetricsCompareClassifier.MatchesFilter(change.Kind, "improvement").Should().BeFalse();
        MetricsCompareClassifier.MatchesFilter(change.Kind, "degradation").Should().BeFalse();
    }

    [Theory]
    [InlineData(0, "—")]
    [InlineData(15, "15")]
    public void Patient_count_renders_an_em_dash_when_none_is_stored(int count, string text)
    {
        new MetricsRunListItem { PatientCount = count }.PatientCountText.Should().Be(text);
    }

    [Theory]
    [InlineData(MetricsChangeKind.Improvement, "improvement", true)]
    [InlineData(MetricsChangeKind.Improvement, "IMPROVEMENT", true)]
    [InlineData(MetricsChangeKind.Degradation, "improvement", false)]
    [InlineData(MetricsChangeKind.Unchanged, "improvement", false)]
    [InlineData(MetricsChangeKind.Unclassified, "improvement", false)]
    [InlineData(MetricsChangeKind.Degradation, "degradation", true)]
    [InlineData(MetricsChangeKind.Improvement, "degradation", false)]
    [InlineData(MetricsChangeKind.Unchanged, "degradation", false)]
    [InlineData(MetricsChangeKind.Unclassified, null, true)]
    [InlineData(MetricsChangeKind.Improvement, "all", true)]
    public void Filter_membership_keeps_only_the_matching_kind(MetricsChangeKind kind, string? filter, bool expected)
    {
        MetricsCompareClassifier.MatchesFilter(kind, filter).Should().Be(expected);
    }
}
