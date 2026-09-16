using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Extensions;

namespace UnitTests.Shared;

[Trait("Category", "UnitTests")]
public class ScheduleStatusExtensionsTests
{
    [Theory]
    [InlineData(ScheduleStatus.Submitted)]
    [InlineData(ScheduleStatus.CompletedNotSubmitted)]
    public void IsTerminal_TerminalStatus_ReturnsTrue(ScheduleStatus status)
    {
        Assert.True(status.IsTerminal());
    }

    [Theory]
    [InlineData(ScheduleStatus.New)]
    [InlineData(ScheduleStatus.Scheduled)]
    [InlineData(ScheduleStatus.EndOfPeriod)]
    public void IsTerminal_InProgressStatus_ReturnsFalse(ScheduleStatus status)
    {
        Assert.False(status.IsTerminal());
    }

    /// <summary>
    /// Guards the seven call sites that ask whether a schedule is finished. A new
    /// ScheduleStatus member defaults to non-terminal, which is silent: the report
    /// simply never completes. This fails the moment a member is added without
    /// deciding which side of the line it falls on.
    /// </summary>
    [Fact]
    public void IsTerminal_ClassifiesEveryDeclaredStatus()
    {
        var expected = new Dictionary<ScheduleStatus, bool>
        {
            [ScheduleStatus.New] = false,
            [ScheduleStatus.Scheduled] = false,
            [ScheduleStatus.EndOfPeriod] = false,
            [ScheduleStatus.Submitted] = true,
            [ScheduleStatus.CompletedNotSubmitted] = true
        };

        var declared = Enum.GetValues<ScheduleStatus>();

        var unclassified = declared.Where(status => !expected.ContainsKey(status)).ToList();
        Assert.True(
            unclassified.Count == 0,
            $"ScheduleStatus gained {string.Join(", ", unclassified)} without deciding whether it is terminal. " +
            "Add it to this test and to ScheduleStatusExtensions.IsTerminal.");

        foreach (var status in declared)
        {
            Assert.Equal(expected[status], status.IsTerminal());
        }
    }

    /// <summary>
    /// Cancellation is ReportSchedule.IsDeleted, not a ScheduleStatus member, so a
    /// canceled report still carries whichever status it reached. IsTerminal must not
    /// be read as "this report is over".
    /// </summary>
    [Fact]
    public void IsTerminal_HasNoCanceledMemberToClassify()
    {
        Assert.DoesNotContain(
            Enum.GetNames<ScheduleStatus>(),
            name => name.Contains("Cancel", StringComparison.OrdinalIgnoreCase));
    }
}
