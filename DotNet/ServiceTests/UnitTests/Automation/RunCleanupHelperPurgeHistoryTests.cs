using LantanaGroup.Automation;
using LantanaGroup.Automation.Helpers;
using LantanaGroup.Link.Automation.Link.Helpers;
using LantanaGroup.Link.Automation.Link.Models;
using LantanaGroup.Link.Sdk.ApiClient;
using LantanaGroup.Link.Sdk.Clients;
using Moq;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.Automation;

[Trait("Category", "UnitTests")]
public class RunCleanupHelperPurgeHistoryTests
{
    private static LinkApiResponse Ok() => new() { StatusCode = 200 };
    private static LinkApiResponse NotFound() => new() { StatusCode = 404 };

    [Fact]
    public async Task PurgeRunHistoryAsync_soft_deletes_report_then_deletes_run_when_ReportId_set()
    {
        var runId = Guid.NewGuid();
        var reportId = Guid.NewGuid().ToString();
        var run = new AutomationRunSummary { RunId = runId, ReportId = reportId };

        var report = new Mock<IReportServiceClient>();
        report.Setup(c => c.SoftDeleteScheduleAsync(reportId, It.IsAny<CancellationToken>(), true)).ReturnsAsync(Ok());

        var snapshots = new Mock<ISnapshotStore>();
        snapshots.Setup(s => s.DeleteRunAsync(runId, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        await RunCleanupHelper.PurgeRunHistoryAsync(
            Mock.Of<IFacilityServiceClient>(),
            Mock.Of<INormalizationServiceClient>(),
            Mock.Of<IDataAcquisitionServiceClient>(),
            Mock.Of<IQueryDispatchServiceClient>(),
            Mock.Of<ICensusServiceClient>(),
            report.Object,
            null,
            snapshots.Object,
            Mock.Of<IAutomationOutput>(),
            run,
            TimeSpan.FromDays(14),
            CancellationToken.None);

        report.Verify(c => c.SoftDeleteScheduleAsync(reportId, It.IsAny<CancellationToken>(), true), Times.Once);
        snapshots.Verify(s => s.DeleteRunAsync(runId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PurgeRunHistoryAsync_skips_soft_delete_when_ReportId_missing()
    {
        var runId = Guid.NewGuid();
        var run = new AutomationRunSummary { RunId = runId, ReportId = null };

        var report = new Mock<IReportServiceClient>();
        var snapshots = new Mock<ISnapshotStore>();
        snapshots.Setup(s => s.DeleteRunAsync(runId, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        await RunCleanupHelper.PurgeRunHistoryAsync(
            Mock.Of<IFacilityServiceClient>(),
            Mock.Of<INormalizationServiceClient>(),
            Mock.Of<IDataAcquisitionServiceClient>(),
            Mock.Of<IQueryDispatchServiceClient>(),
            Mock.Of<ICensusServiceClient>(),
            report.Object,
            null,
            snapshots.Object,
            Mock.Of<IAutomationOutput>(),
            run,
            TimeSpan.FromDays(14),
            CancellationToken.None);

        report.Verify(c => c.SoftDeleteScheduleAsync(It.IsAny<string>(), It.IsAny<CancellationToken>(), It.IsAny<bool>()), Times.Never);
        snapshots.Verify(s => s.DeleteRunAsync(runId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PurgeRunHistoryAsync_treats_missing_report_schedule_as_success()
    {
        var runId = Guid.NewGuid();
        var reportId = Guid.NewGuid().ToString();
        var run = new AutomationRunSummary { RunId = runId, ReportId = reportId };

        var report = new Mock<IReportServiceClient>();
        report.Setup(c => c.SoftDeleteScheduleAsync(reportId, It.IsAny<CancellationToken>(), true)).ReturnsAsync(NotFound());

        var snapshots = new Mock<ISnapshotStore>();
        snapshots.Setup(s => s.DeleteRunAsync(runId, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        await RunCleanupHelper.PurgeRunHistoryAsync(
            Mock.Of<IFacilityServiceClient>(),
            Mock.Of<INormalizationServiceClient>(),
            Mock.Of<IDataAcquisitionServiceClient>(),
            Mock.Of<IQueryDispatchServiceClient>(),
            Mock.Of<ICensusServiceClient>(),
            report.Object,
            null,
            snapshots.Object,
            Mock.Of<IAutomationOutput>(),
            run,
            TimeSpan.FromDays(14),
            CancellationToken.None);

        snapshots.Verify(s => s.DeleteRunAsync(runId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PurgeRunHistoryAsync_skips_facility_teardown_for_non_guid_facility()
    {
        var runId = Guid.NewGuid();
        var run = new AutomationRunSummary { RunId = runId, FacilityId = "echs", ReportId = null };

        var dataAcq = new Mock<IDataAcquisitionServiceClient>(MockBehavior.Strict);
        var snapshots = new Mock<ISnapshotStore>();
        snapshots.Setup(s => s.DeleteRunAsync(runId, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        await RunCleanupHelper.PurgeRunHistoryAsync(
            Mock.Of<IFacilityServiceClient>(),
            Mock.Of<INormalizationServiceClient>(),
            dataAcq.Object,
            Mock.Of<IQueryDispatchServiceClient>(),
            Mock.Of<ICensusServiceClient>(),
            Mock.Of<IReportServiceClient>(),
            null,
            snapshots.Object,
            Mock.Of<IAutomationOutput>(),
            run,
            TimeSpan.FromDays(14),
            CancellationToken.None);

        dataAcq.VerifyNoOtherCalls();
        snapshots.Verify(s => s.DeleteRunAsync(runId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PurgeRunHistoryAsync_skips_soft_delete_for_non_guid_ReportId()
    {
        var runId = Guid.NewGuid();
        var run = new AutomationRunSummary { RunId = runId, ReportId = "seed-report-demo" };

        var report = new Mock<IReportServiceClient>();
        var snapshots = new Mock<ISnapshotStore>();
        snapshots.Setup(s => s.DeleteRunAsync(runId, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        await RunCleanupHelper.PurgeRunHistoryAsync(
            Mock.Of<IFacilityServiceClient>(),
            Mock.Of<INormalizationServiceClient>(),
            Mock.Of<IDataAcquisitionServiceClient>(),
            Mock.Of<IQueryDispatchServiceClient>(),
            Mock.Of<ICensusServiceClient>(),
            report.Object,
            null,
            snapshots.Object,
            Mock.Of<IAutomationOutput>(),
            run,
            TimeSpan.FromDays(14),
            CancellationToken.None,
            teardownFacility: false);

        report.Verify(c => c.SoftDeleteScheduleAsync(It.IsAny<string>(), It.IsAny<CancellationToken>(), It.IsAny<bool>()), Times.Never);
        snapshots.Verify(s => s.DeleteRunAsync(runId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PurgeRunHistoryAsync_skips_facility_teardown_when_teardownFacility_false()
    {
        var runId = Guid.NewGuid();
        var facilityId = Guid.NewGuid().ToString();
        var run = new AutomationRunSummary { RunId = runId, FacilityId = facilityId, ReportId = null };

        var dataAcq = new Mock<IDataAcquisitionServiceClient>(MockBehavior.Strict);
        var snapshots = new Mock<ISnapshotStore>();
        snapshots.Setup(s => s.DeleteRunAsync(runId, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        await RunCleanupHelper.PurgeRunHistoryAsync(
            Mock.Of<IFacilityServiceClient>(),
            Mock.Of<INormalizationServiceClient>(),
            dataAcq.Object,
            Mock.Of<IQueryDispatchServiceClient>(),
            Mock.Of<ICensusServiceClient>(),
            Mock.Of<IReportServiceClient>(),
            null,
            snapshots.Object,
            Mock.Of<IAutomationOutput>(),
            run,
            TimeSpan.FromDays(14),
            CancellationToken.None,
            teardownFacility: false);

        dataAcq.VerifyNoOtherCalls();
        snapshots.Verify(s => s.DeleteRunAsync(runId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PurgeRunHistoryAsync_skips_facility_teardown_when_already_torn_down()
    {
        var runId = Guid.NewGuid();
        var facilityId = Guid.NewGuid().ToString();
        var run = new AutomationRunSummary { RunId = runId, FacilityId = facilityId, ReportId = null };

        var dataAcq = new Mock<IDataAcquisitionServiceClient>(MockBehavior.Strict);
        var snapshots = new Mock<ISnapshotStore>();
        snapshots.Setup(s => s.DeleteRunAsync(runId, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        await RunCleanupHelper.PurgeRunHistoryAsync(
            Mock.Of<IFacilityServiceClient>(),
            Mock.Of<INormalizationServiceClient>(),
            dataAcq.Object,
            Mock.Of<IQueryDispatchServiceClient>(),
            Mock.Of<ICensusServiceClient>(),
            Mock.Of<IReportServiceClient>(),
            null,
            snapshots.Object,
            Mock.Of<IAutomationOutput>(),
            run,
            TimeSpan.FromDays(14),
            CancellationToken.None,
            teardownFacility: true,
            alreadyTornDownFacilityIds: new HashSet<string>(StringComparer.OrdinalIgnoreCase) { facilityId });

        dataAcq.VerifyNoOtherCalls();
        snapshots.Verify(s => s.DeleteRunAsync(runId, It.IsAny<CancellationToken>()), Times.Once);
    }
}
