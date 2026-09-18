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

    [Fact]
    public void The_default_cron_names_a_nominal_local_time_of_23_59()
    {
        new DmrpSchedulingSettings().ResolvedNightlyLocalTime.Should().Be(new TimeSpan(23, 59, 0));
    }

    [Fact]
    public void A_cron_whose_second_minute_and_hour_are_plain_numbers_names_its_local_time()
    {
        new DmrpSchedulingSettings { NightlyCron = "0 0 21 * * ?" }.ResolvedNightlyLocalTime
            .Should().Be(new TimeSpan(21, 0, 0));
    }

    [Theory]
    [InlineData("0 */5 * * * ?")]
    [InlineData("0 0 21,22 * * ?")]
    [InlineData("0 0 8-17 * * ?")]
    public void A_cron_that_fires_at_more_than_one_time_of_day_names_none(string cron)
    {
        new DmrpSchedulingSettings { NightlyCron = cron }.ResolvedNightlyLocalTime.Should().BeNull();
    }

    [Fact]
    public void An_invalid_cron_names_the_local_time_of_the_default_it_falls_back_to()
    {
        new DmrpSchedulingSettings { NightlyCron = "not a cron" }.ResolvedNightlyLocalTime
            .Should().Be(new TimeSpan(23, 59, 0));
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

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not a date")]
    public void An_unset_or_unparseable_override_resolves_to_null(string? value)
    {
        new DmrpSchedulingSettings { ScheduledFireTimeOverride = value }
            .ResolvedScheduledFireTimeOverride.Should().BeNull();
    }

    [Fact]
    public void An_override_with_a_Z_suffix_resolves_to_that_utc_instant()
    {
        new DmrpSchedulingSettings { ScheduledFireTimeOverride = "2026-10-01T03:59:00Z" }
            .ResolvedScheduledFireTimeOverride.Should().Be(new DateTimeOffset(2026, 10, 1, 3, 59, 0, TimeSpan.Zero));
    }

    [Fact]
    public void An_override_with_an_explicit_zero_offset_resolves_to_the_same_instant_as_Z()
    {
        new DmrpSchedulingSettings { ScheduledFireTimeOverride = "2026-10-01T03:59:00+00:00" }
            .ResolvedScheduledFireTimeOverride.Should().Be(new DateTimeOffset(2026, 10, 1, 3, 59, 0, TimeSpan.Zero));
    }

    [Fact]
    public void An_override_with_a_non_utc_offset_is_adjusted_to_the_same_utc_instant()
    {
        new DmrpSchedulingSettings { ScheduledFireTimeOverride = "2026-09-30T23:59:00-04:00" }
            .ResolvedScheduledFireTimeOverride.Should().Be(new DateTimeOffset(2026, 10, 1, 3, 59, 0, TimeSpan.Zero));
    }
}
