using FluentAssertions;
using LantanaGroup.Link.DMRP.Config;

namespace UnitTests.DMRP;

[Trait("Category", "UnitTests")]
public class DmrpSchedulingSettingsTests
{
    [Fact]
    public void Defaults_are_nightly_at_23_59_with_four_workers_and_three_catch_up_nights()
    {
        var settings = new DmrpSchedulingSettings();

        settings.ResolvedNightlyCron.Should().Be("0 59 23 * * ?");
        settings.ResolvedConcurrency.Should().Be(4);
        settings.ResolvedCatchUpNights.Should().Be(3);
    }

    [Theory]
    [InlineData("not a cron")]
    [InlineData("")]
    [InlineData(null)]
    public void An_invalid_cron_falls_back_to_the_default(string? cron)
    {
        new DmrpSchedulingSettings { NightlyCron = cron! }.ResolvedNightlyCron.Should().Be("0 59 23 * * ?");
    }

    [Fact]
    public void A_valid_cron_is_kept()
    {
        new DmrpSchedulingSettings { NightlyCron = "0 0 21 * * ?" }.ResolvedNightlyCron.Should().Be("0 0 21 * * ?");
    }

    [Theory]
    [InlineData(0, 4)]
    [InlineData(33, 4)]
    [InlineData(1, 1)]
    [InlineData(32, 32)]
    public void Concurrency_outside_1_to_32_falls_back(int configured, int expected)
    {
        new DmrpSchedulingSettings { Concurrency = configured }.ResolvedConcurrency.Should().Be(expected);
    }

    [Theory]
    [InlineData(-1, 3)]
    [InlineData(29, 3)]
    [InlineData(0, 0)]
    [InlineData(28, 28)]
    public void CatchUpNights_outside_0_to_28_falls_back(int configured, int expected)
    {
        new DmrpSchedulingSettings { CatchUpNights = configured }.ResolvedCatchUpNights.Should().Be(expected);
    }
}
