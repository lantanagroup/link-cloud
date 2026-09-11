using LantanaGroup.Automation.Helpers;
using LantanaGroup.Link.Automation.Link.Helpers;
using LantanaGroup.Link.Sdk.Clients;
using Moq;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.Automation;

/// <summary>
/// Progress accounting for a bypassed report. Covers the case where every patient ends on
/// NotSubmitted rather than Submitted: the run is finished, so it must read as complete
/// rather than sitting short of 100% until stall detection fires.
/// </summary>
[Trait("Category", "UnitTests")]
public class PipelineProgressTrackerTests
{
    private const string FacilityId = "facility-a";
    private static readonly Guid ScheduleId = Guid.NewGuid();

    [Theory]
    [InlineData("Submitted")]
    [InlineData("NotSubmitted")]
    public async Task UpdateAsync_TerminalSubmissionStatus_ReportsFullProgress(string submissionStatus)
    {
        var output = new CapturingOutput();
        var reader = BuildReader(
            scheduleStatus: submissionStatus == "NotSubmitted" ? "CompletedNotSubmitted" : "Submitted",
            entrySubmissionStatus: submissionStatus,
            patientCount: 2);

        var tracker = new PipelineProgressTracker(output, expectedPatientCount: 2, reader.Object, expectsDataAcquisition: false);

        await tracker.UpdateAsync(FacilityId, ScheduleId.ToString());

        var line = Assert.Single(output.Lines);
        Assert.Contains("100%", line);
        Assert.Contains("submit=2/2", line);
    }

    /// <summary>
    /// The regression this guards. Before NotSubmitted counted as terminal, a fully bypassed
    /// report stalled at submit=0/N -- a completed run that looked hung.
    /// </summary>
    [Fact]
    public async Task UpdateAsync_BypassedReport_DoesNotStallOnSubmissionStage()
    {
        var output = new CapturingOutput();
        var reader = BuildReader("CompletedNotSubmitted", "NotSubmitted", patientCount: 3);

        var tracker = new PipelineProgressTracker(output, expectedPatientCount: 3, reader.Object, expectsDataAcquisition: false);

        await tracker.UpdateAsync(FacilityId, ScheduleId.ToString());

        var line = Assert.Single(output.Lines);
        Assert.DoesNotContain("submit=0/3", line);
        Assert.Contains("submit=3/3", line);
    }

    /// <summary>
    /// A patient still mid-flight must not be counted, or the tracker would report completion
    /// for a run that has not finished.
    /// </summary>
    [Fact]
    public async Task UpdateAsync_SubmissionStillInProgress_IsNotCounted()
    {
        var output = new CapturingOutput();
        var reader = BuildReader("EndOfPeriod", "Submitting", patientCount: 2);

        var tracker = new PipelineProgressTracker(output, expectedPatientCount: 2, reader.Object, expectsDataAcquisition: false);

        await tracker.UpdateAsync(FacilityId, ScheduleId.ToString());

        var line = Assert.Single(output.Lines);
        Assert.Contains("submit=0/2", line);
        Assert.DoesNotContain("100%", line);
    }

    private static Mock<PipelineDataReader> BuildReader(
        string scheduleStatus,
        string entrySubmissionStatus,
        int patientCount)
    {
        // Concrete class with the two consumed methods virtual, so no HTTP is reachable.
        var reader = new Mock<PipelineDataReader>(
            Mock.Of<IReportServiceClient>(),
            Mock.Of<IDataAcquisitionServiceClient>(),
            Mock.Of<INormalizationServiceClient>(),
            Mock.Of<IFacilityServiceClient>())
        { CallBase = false };

        reader.Setup(r => r.GetReportScheduleAsync(It.IsAny<Guid>()))
            .ReturnsAsync(new PipelineDataReader.ReportScheduleInfo(
                FacilityId, scheduleStatus, "Adhoc", "Manual",
                EnableSubmission: scheduleStatus != "CompletedNotSubmitted",
                EndOfReportPeriodJobHasRun: true,
                PayloadRootUri: "https://blob.example.com/report",
                ReportStartDate: DateTime.UtcNow.AddDays(-30),
                ReportEndDate: DateTime.UtcNow.AddDays(30),
                CreateDate: DateTime.UtcNow,
                SubmitReportDateTime: null));

        var entries = Enumerable.Range(1, patientCount)
            .Select(i => new PipelineDataReader.ReportEntryInfo(
                Guid.NewGuid(), FacilityId, $"patient-{i}",
                ReportingStatus: "PassedValidation",
                SubmissionStatus: entrySubmissionStatus,
                MeasureReports:
                [
                    new PipelineDataReader.MeasureReportInfo($"mr-{i}", "ReadyForValidation", "DE-111", [])
                ]))
            .ToList();

        reader.Setup(r => r.GetReportEntriesWithMeasureReportsAsync(It.IsAny<Guid>()))
            .ReturnsAsync(entries);

        return reader;
    }

    private sealed class CapturingOutput : IAutomationOutput
    {
        public List<string> Lines { get; } = [];

        public void WriteLine(string message) => Lines.Add(message);

        public void WriteLine(string format, params object[] args) => Lines.Add(string.Format(format, args));
    }
}
