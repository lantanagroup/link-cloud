using Automation.UI.Services;
using FluentAssertions;
using LantanaGroup.Link.Automation.Link.Helpers;
using LantanaGroup.Link.Automation.Link.Models;
using LantanaGroup.Link.Sdk.Clients;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.AutomationUI;

[Trait("Category", "UnitTests")]
public class StoreBackedServicePollerTests
{
    [Fact]
    public async Task FinalPollAsync_skips_the_fetch_when_the_stored_report_does_not_match()
    {
        var runId = Guid.NewGuid();
        var reportId = Guid.NewGuid().ToString();
        var reader = new Mock<PipelineDataReader>(
            Mock.Of<IReportServiceClient>(),
            Mock.Of<IDataAcquisitionServiceClient>(),
            Mock.Of<INormalizationServiceClient>(),
            Mock.Of<IFacilityServiceClient>());
        var store = new Mock<ISnapshotStore>();
        store.Setup(s => s.GetRunMetaAsync(runId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RunSnapshotMeta
            {
                RunId = runId,
                FacilityId = "facility",
                ReportId = Guid.NewGuid().ToString()
            });

        var poller = new StoreBackedServicePoller(
            store.Object,
            reader.Object,
            new RunSnapshotMeta
            {
                RunId = runId,
                FacilityId = "facility",
                ReportId = reportId,
                StartedAt = DateTimeOffset.UtcNow
            },
            NullLogger.Instance);

        await poller.FinalPollAsync();

        reader.Verify(
            r => r.GetReportScheduleAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
        store.Verify(
            s => s.SetDomainAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<It.IsAnyType>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
