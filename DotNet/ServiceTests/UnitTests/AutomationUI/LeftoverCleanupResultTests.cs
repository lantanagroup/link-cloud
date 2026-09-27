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

    [Fact]
    public void ProcessedAllCandidates_is_true_when_nothing_matched()
        => Empty().ProcessedAllCandidates.Should().BeTrue();

    [Fact]
    public void ProcessedAllCandidates_is_false_when_the_pass_is_capped()
        => new LeftoverCleanupResult(100, ["a"], 0, [], 0, [], [], []).ProcessedAllCandidates.Should().BeFalse();

    [Fact]
    public void ProcessedAllCandidates_is_true_when_every_quiesce_candidate_was_attempted()
        => new LeftoverCleanupResult(2, ["a"], 0, [], 0, [], ["b"], []).ProcessedAllCandidates.Should().BeTrue();

    [Fact]
    public void ProcessedAllCandidates_is_true_when_every_teardown_candidate_was_attempted()
        => new LeftoverCleanupResult(0, [], 2, ["a"], 0, [], ["b"], []).ProcessedAllCandidates.Should().BeTrue();

    [Fact]
    public void ProcessedAllCandidates_is_false_when_history_purge_is_capped()
        => new LeftoverCleanupResult(0, [], 0, [], 5, [Guid.NewGuid()], [], []).ProcessedAllCandidates.Should().BeFalse();

    private static LeftoverCleanupResult Empty()
        => new(0, [], 0, [], 0, [], [], []);
}
