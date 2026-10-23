using Automation.UI.Models;
using FluentAssertions;

namespace UnitTests.AutomationUI;

[Trait("Category", "UnitTests")]
public class CleanupStatusBadgeTests
{
    [Theory]
    [InlineData("failed", CleanupStatusBadge.Failed)]
    [InlineData("completed", CleanupStatusBadge.Completed)]
    [InlineData("running", CleanupStatusBadge.Completed)]
    [InlineData("", CleanupStatusBadge.Completed)]
    [InlineData(null, CleanupStatusBadge.Completed)]
    public void ReportStatus_UsesABootstrapBackground(string? status, string expected)
        => CleanupStatusBadge.ForReport(status).Should().Be(expected);

    [Theory]
    [InlineData("running", CleanupStatusBadge.Running)]
    [InlineData("completed", CleanupStatusBadge.Completed)]
    [InlineData("failed", CleanupStatusBadge.Failed)]
    [InlineData("idle", CleanupStatusBadge.Idle)]
    [InlineData("unexpected", CleanupStatusBadge.Idle)]
    [InlineData(null, CleanupStatusBadge.Idle)]
    public void ActivityStatus_UsesABootstrapBackground(string? status, string expected)
        => CleanupStatusBadge.ForActivity(status).Should().Be(expected);

    [Theory]
    [InlineData("running", CleanupStatusBadge.RunningLabel)]
    [InlineData("completed", CleanupStatusBadge.CompletedLabel)]
    [InlineData("failed", CleanupStatusBadge.FailedLabel)]
    [InlineData("idle", CleanupStatusBadge.IdleLabel)]
    [InlineData(null, CleanupStatusBadge.IdleLabel)]
    public void ActivityLabel_MatchesTheLiveBadgeText(string? status, string expected)
        => CleanupStatusBadge.ActivityLabel(status).Should().Be(expected);

    [Fact]
    public void Classes_AreBootstrap51BackgroundUtilities()
    {
        CleanupStatusBadge.Failed.Should().Be("bg-danger");
        CleanupStatusBadge.Completed.Should().Be("bg-success");
        CleanupStatusBadge.Running.Should().Be("bg-primary");
        CleanupStatusBadge.Idle.Should().Be("bg-secondary");
    }
}
