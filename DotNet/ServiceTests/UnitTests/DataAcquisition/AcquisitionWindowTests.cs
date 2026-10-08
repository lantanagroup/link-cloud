using FluentAssertions;
using LantanaGroup.Link.DataAcquisition.Jobs;
using Xunit;

namespace ServiceTests.UnitTests.DataAcquisition;

public class AcquisitionWindowTests
{
    private static readonly TimeSpan SixPm = new(18, 0, 0);
    private static readonly TimeSpan SixAm = new(6, 0, 0);
    private static readonly TimeSpan ElevenPm = new(23, 0, 0);
    private static readonly TimeSpan ThreeAm = new(3, 0, 0);
    private static readonly TimeSpan Noon = new(12, 0, 0);

    [Theory]
    [InlineData(23, 0, 0, true)]
    [InlineData(3, 0, 0, true)]
    [InlineData(12, 0, 0, false)]
    [InlineData(18, 0, 0, true)]
    [InlineData(6, 0, 0, true)]
    public void Overnight_window_includes_both_sides_of_midnight_and_the_boundaries(int hour, int minute, int second, bool inside)
    {
        AcquisitionWindow.Contains(SixPm, SixAm, new TimeSpan(hour, minute, second)).Should().Be(inside);
    }

    [Fact]
    public void Same_day_window_stays_inclusive_and_excludes_the_outside()
    {
        AcquisitionWindow.Contains(SixAm, SixPm, SixAm).Should().BeTrue();
        AcquisitionWindow.Contains(SixAm, SixPm, SixPm).Should().BeTrue();
        AcquisitionWindow.Contains(SixAm, SixPm, Noon).Should().BeTrue();
        AcquisitionWindow.Contains(SixAm, SixPm, ElevenPm).Should().BeFalse();
        AcquisitionWindow.Contains(SixAm, SixPm, ThreeAm).Should().BeFalse();
    }

    [Fact]
    public void Equal_bounds_match_only_that_instant()
    {
        AcquisitionWindow.Contains(SixPm, SixPm, SixPm).Should().BeTrue();
        AcquisitionWindow.Contains(SixPm, SixPm, SixPm.Add(TimeSpan.FromSeconds(1))).Should().BeFalse();
        AcquisitionWindow.Contains(SixPm, SixPm, Noon).Should().BeFalse();
    }

    [Fact]
    public void Both_null_is_unrestricted()
    {
        AcquisitionWindow.Contains(null, null, Noon).Should().BeTrue();
        AcquisitionWindow.Contains(null, null, TimeSpan.Zero).Should().BeTrue();
    }
}
