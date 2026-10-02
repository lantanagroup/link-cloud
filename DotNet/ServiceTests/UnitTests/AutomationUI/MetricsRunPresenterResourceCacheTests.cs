using Automation.UI.Services;
using Automation.UI.Services.Persistence;
using FluentAssertions;

namespace UnitTests.AutomationUI;

[Trait("Category", "UnitTests")]
public class MetricsRunPresenterResourceCacheTests
{
    [Fact]
    public void ToResourceCache_WithMeasureEvaluationReads_ShowsThemApartFromTheCombinedReads()
    {
        var document = new AutomationRunMetricsDocument
        {
            ResourceCache = new ResourceCacheSnapshot
            {
                Unavailable = false,
                HitCount = 100,
                FallbackCount = 20,
                HitRatio = 100.0 / 120,
                EvaluationHitCount = 30,
                EvaluationFallbackCount = 10,
                EvaluationEmptyCount = 2,
                EvaluationHitRatio = 0.75,
                EvaluationMissCount = 6,
                EvaluationPartialCount = 3,
                EvaluationUnavailableCount = 1
            }
        };

        var view = MetricsRunPresenter.ToResourceCache(document);

        view.Unavailable.Should().BeFalse();
        view.HitRatioPercent.Should().BeApproximately(83.33, 0.01);

        view.EvaluationUnavailable.Should().BeFalse();
        view.EvaluationHitRatioPercent.Should().BeApproximately(75, 0.0001);
        view.EvaluationHitCount.Should().Be(30);
        view.EvaluationFallbackCount.Should().Be(10);
        view.EvaluationEmptyCount.Should().Be(2);
        view.EvaluationMissCount.Should().Be(6);
        view.EvaluationPartialCount.Should().Be(3);
        view.EvaluationUnavailableCount.Should().Be(1);
    }

    [Fact]
    public void ToResourceCache_WithoutMeasureEvaluationReads_MarksThemUnavailable()
    {
        // Runs captured before evaluation reads were recorded, or with MeasureEval not exporting them.
        var document = new AutomationRunMetricsDocument
        {
            ResourceCache = new ResourceCacheSnapshot { Unavailable = false, HitCount = 100, HitRatio = 0.9 }
        };

        var view = MetricsRunPresenter.ToResourceCache(document);

        view.Unavailable.Should().BeFalse();
        view.EvaluationUnavailable.Should().BeTrue();
        view.EvaluationHitRatioPercent.Should().BeNull();
    }
}
