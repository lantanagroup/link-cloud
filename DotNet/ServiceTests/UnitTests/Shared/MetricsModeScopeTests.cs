using LantanaGroup.Link.Shared.Application.Utilities;

namespace UnitTests.Shared;

[Trait("Category", "UnitTests")]
public class MetricsModeScopeTests
{
    [Fact]
    public void Begin_SetsAndRestoresPerformanceFlag()
    {
        Assert.False(MetricsModeScope.IsPerformance);

        using (MetricsModeScope.Begin(true))
        {
            Assert.True(MetricsModeScope.IsPerformance);
        }

        Assert.False(MetricsModeScope.IsPerformance);
    }

    [Fact]
    public void NestedBegin_RestoresPreviousValue()
    {
        using (MetricsModeScope.Begin(true))
        {
            using (MetricsModeScope.Begin(false))
            {
                Assert.False(MetricsModeScope.IsPerformance);
            }

            Assert.True(MetricsModeScope.IsPerformance);
        }

        Assert.False(MetricsModeScope.IsPerformance);
    }
}
