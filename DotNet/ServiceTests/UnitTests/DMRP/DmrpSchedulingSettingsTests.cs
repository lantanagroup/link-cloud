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

    [Theory]
    [InlineData("2026-09-30T23:59:00")]
    [InlineData("2026-09-30T23:59")]
    [InlineData("2026-09-30 23:59:00")]
    public void An_override_without_an_offset_resolves_to_that_local_wall_clock_time(string value)
    {
        var resolved = new DmrpSchedulingSettings { ScheduledFireTimeOverride = value }.ResolvedScheduledFireTimeOverride;

        resolved.Should().Be(new DateTime(2026, 9, 30, 23, 59, 0));
        resolved!.Value.Kind.Should().Be(DateTimeKind.Unspecified);
    }

    [Theory]
    [InlineData("2026-10-01T03:59:00Z")]
    [InlineData("2026-10-01T03:59:00+00:00")]
    [InlineData("2026-09-30T23:59:00-04:00")]
    public void An_override_carrying_an_offset_is_rejected_rather_than_guessed(string value)
    {
        // The value is read in each facility timezone; an instant would mean a different local
        // night per zone, so it is refused instead of silently converted.
        var settings = new DmrpSchedulingSettings { ScheduledFireTimeOverride = value };

        settings.ResolvedScheduledFireTimeOverride.Should().BeNull();
        settings.ScheduledFireTimeOverrideIsInvalid.Should().BeTrue();
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("not a date", true)]
    public void Invalid_means_set_but_unusable(string? value, bool expected)
    {
        new DmrpSchedulingSettings { ScheduledFireTimeOverride = value }
            .ScheduledFireTimeOverrideIsInvalid.Should().Be(expected);
    }
}
