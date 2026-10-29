using Automation.UI.Services;
using FluentAssertions;

namespace UnitTests.AutomationUI;

[Trait("Category", "UnitTests")]
public class RunOrganizationIdAllocatorTests
{
    [Fact]
    public void Allocate_returns_a_five_digit_id()
    {
        var id = RunOrganizationIdAllocator.Allocate(new HashSet<string>());

        id.Should().MatchRegex("^\\d{5}$");
        int.Parse(id).Should().BeInRange(
            RunOrganizationIdAllocator.Minimum,
            RunOrganizationIdAllocator.ExclusiveMaximum - 1);
    }

    [Fact]
    public void Allocate_skips_an_id_an_ongoing_run_already_uses()
    {
        var draws = new Queue<int>([10_000, 10_001]);
        var inUse = new HashSet<string>(StringComparer.Ordinal) { "10000" };

        var id = RunOrganizationIdAllocator.Allocate(inUse, (_, _) => draws.Dequeue());

        id.Should().Be("10001");
    }

    [Fact]
    public void Allocate_fails_when_every_draw_is_already_in_use()
    {
        var inUse = new HashSet<string>(StringComparer.Ordinal) { "10000" };

        var act = () => RunOrganizationIdAllocator.Allocate(inUse, (_, _) => 10_000);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*ongoing run*");
    }

    [Fact]
    public void CollectInUse_ignores_a_finished_run_and_keeps_an_ongoing_one()
    {
        var inUse = RunOrganizationIdAllocator.CollectInUse(
        [
            ("48291", "48291", Ongoing: false),
            ("10001", null, Ongoing: true),
            (null, "10002", Ongoing: true),
            ("  ", "10003", Ongoing: false)
        ]);

        inUse.Should().BeEquivalentTo(["10001", "10002"]);
    }

    [Fact]
    public void Two_allocations_do_not_share_an_id_once_the_first_is_reserved()
    {
        var inUse = new HashSet<string>(StringComparer.Ordinal);
        var draws = new Queue<int>([22_222, 22_222, 33_333]);

        var first = RunOrganizationIdAllocator.Allocate(inUse, (_, _) => draws.Dequeue());
        inUse.Add(first);
        var second = RunOrganizationIdAllocator.Allocate(inUse, (_, _) => draws.Dequeue());

        first.Should().Be("22222");
        second.Should().Be("33333");
    }
}
