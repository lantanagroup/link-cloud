using LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition;
using RequestStatus = LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition.RequestStatus;

namespace ServiceTests.UnitTests.DataAcquisition;

[Trait("Category", "UnitTests")]
public class RequestStatusExtensionsTests
{
    private static readonly RequestStatus[] ExpectedTerminal =
    [
        RequestStatus.Completed,
        RequestStatus.MaxRetriesReached,
        RequestStatus.Skipped,
        RequestStatus.Cancelled,
        RequestStatus.ConfigurationMissing,
        RequestStatus.NotReportable,
    ];

    private static readonly RequestStatus[] ExpectedCancellable =
    [
        RequestStatus.Pending,
        RequestStatus.Ready,
        RequestStatus.Queued,
        RequestStatus.Processing,
        RequestStatus.Failed,
        RequestStatus.ConfigurationRequired,
    ];

    [Fact]
    public void TerminalStatuses_MatchesExpectedClassification()
    {
        Assert.Equal(
            ExpectedTerminal.OrderBy(s => s),
            RequestStatusExtensions.TerminalStatuses.OrderBy(s => s));
    }

    [Fact]
    public void CancellableStatuses_MatchesExpectedClassification()
    {
        Assert.Equal(
            ExpectedCancellable.OrderBy(s => s),
            RequestStatusExtensions.CancellableStatuses.OrderBy(s => s));
    }

    // The durable invariant: "finished" and "cancellable" are independent axes, but a terminal
    // log is never cancellable. If a future status is ever marked both, that's a real defect.
    [Fact]
    public void NoStatusIsBothTerminalAndCancellable()
    {
        var both = RequestStatusExtensions.TerminalStatuses
            .Intersect(RequestStatusExtensions.CancellableStatuses)
            .ToList();

        Assert.True(both.Count == 0, $"Statuses marked both terminal and cancellable: {string.Join(", ", both)}");
    }

    [Theory]
    [InlineData(RequestStatus.Completed, true)]
    [InlineData(RequestStatus.MaxRetriesReached, true)]
    [InlineData(RequestStatus.Skipped, true)]
    [InlineData(RequestStatus.Cancelled, true)]
    [InlineData(RequestStatus.ConfigurationMissing, true)]
    [InlineData(RequestStatus.NotReportable, true)]
    [InlineData(RequestStatus.Pending, false)]
    [InlineData(RequestStatus.Ready, false)]
    [InlineData(RequestStatus.Queued, false)]
    [InlineData(RequestStatus.Processing, false)]
    [InlineData(RequestStatus.Failed, false)]
    [InlineData(RequestStatus.ConfigurationRequired, false)]
    public void IsTerminal_ReturnsExpected(RequestStatus status, bool expected)
    {
        Assert.Equal(expected, status.IsTerminal());
    }

    [Theory]
    [InlineData(RequestStatus.Pending, true)]
    [InlineData(RequestStatus.Ready, true)]
    [InlineData(RequestStatus.Queued, true)]
    [InlineData(RequestStatus.Processing, true)]
    [InlineData(RequestStatus.Failed, true)]
    [InlineData(RequestStatus.ConfigurationRequired, true)]
    [InlineData(RequestStatus.Completed, false)]
    [InlineData(RequestStatus.MaxRetriesReached, false)]
    [InlineData(RequestStatus.Skipped, false)]
    [InlineData(RequestStatus.Cancelled, false)]
    [InlineData(RequestStatus.ConfigurationMissing, false)]
    [InlineData(RequestStatus.NotReportable, false)]
    public void IsCancellable_ReturnsExpected(RequestStatus status, bool expected)
    {
        Assert.Equal(expected, status.IsCancellable());
    }
}
