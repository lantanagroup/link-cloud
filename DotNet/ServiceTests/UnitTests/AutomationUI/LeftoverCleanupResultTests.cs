using Automation.UI.Services;
using FluentAssertions;

namespace UnitTests.AutomationUI;

[Trait("Category", "UnitTests")]
public class LeftoverCleanupResultTests
{
    [Fact]
    public void Succeeded_is_true_when_nothing_failed()
        => Empty().Succeeded.Should().BeTrue();

    [Fact]
    public void Succeeded_is_false_when_a_facility_failed()
        => (Empty() with { FailedFacilityIds = ["leftover"] }).Succeeded.Should().BeFalse();

    [Fact]
    public void Succeeded_is_false_when_a_run_failed()
        => (Empty() with { FailedRunIds = [Guid.NewGuid()] }).Succeeded.Should().BeFalse();

    private static LeftoverCleanupResult Empty()
        => new(0, [], 0, [], 0, [], [], []);
}
