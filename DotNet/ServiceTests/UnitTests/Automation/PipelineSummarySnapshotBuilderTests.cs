using FluentAssertions;
using LantanaGroup.Link.Automation.Link.Helpers;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.Automation;

[Trait("Category", "UnitTests")]
public class PipelineSummarySnapshotBuilderTests
{
    private static readonly Guid ScheduleId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");

    [Fact]
    public async Task Milestones_include_Normalized_between_acquisition_and_measure_eval()
    {
        var snapshot = await BuildAsync(new PipelineSummarySnapshotBuilder.ResolvedDomainData
        {
            Schedule = Schedule("InProgress"),
            AcquisitionSummary = Summary(totalLogs: 2, completed: 2, resources: 10),
            AcquisitionLogs =
            [
                CompletedLog(1, start: Minutes(0), durationMs: 5_000, resources: 4),
                CompletedLog(2, start: Minutes(0.5), durationMs: 5_000, resources: 6)
            ]
        });

        snapshot.Report.Milestones.Select(m => m.Name).Should().Equal(
            "Report Scheduled",
            "Data Acquired",
            "Normalized",
            "Measure Evaluated",
            "Validation",
            "Submitted",
            "Test Validation");
    }

    [Fact]
    public async Task Data_acquisition_rates_use_log_wall_clock_not_run_logs()
    {
        var start = new DateTime(2026, 9, 2, 12, 0, 0, DateTimeKind.Utc);
        var snapshot = await BuildAsync(
            new PipelineSummarySnapshotBuilder.ResolvedDomainData
            {
                Schedule = Schedule("InProgress"),
                AcquisitionSummary = Summary(totalLogs: 2, completed: 2, resources: 20),
                AcquisitionLogs =
                [
                    new PipelineDataReader.AcquisitionLogInfo(
                        1, "p1", null, ScheduleId.ToString(), "Completed", "Initial", [],
                        ["Patient/p1", "Encounter/e1"], [],
                        ExecutionDate: start,
                        CreateDate: start,
                        CompletionDate: start.AddSeconds(10),
                        CompletionTimeMilliseconds: 10_000),
                    new PipelineDataReader.AcquisitionLogInfo(
                        2, "p2", null, ScheduleId.ToString(), "Completed", "Initial", [],
                        Enumerable.Range(0, 18).Select(i => $"Observation/{i}").ToList(), [],
                        ExecutionDate: start.AddSeconds(2),
                        CreateDate: start.AddSeconds(2),
                        CompletionDate: start.AddSeconds(12),
                        CompletionTimeMilliseconds: 10_000)
                ]
            },
            logs:
            [
                "[12:00:00] [Snapshot][DataAcqLog] still polling 20 minutes later",
                "[12:20:00] [Snapshot][DataAcqLog] still polling 20 minutes later"
            ]);

        snapshot.DataAcquisition.ActiveDurationSeconds.Should().Be(12);
        snapshot.DataAcquisition.ResourceCount.Should().Be(20);
        snapshot.DataAcquisition.AverageResourcesPerSecond.Should().Be(Math.Round(20 / 12.0, 2));
        snapshot.DataAcquisition.CompletionRatePerSecond.Should().Be(Math.Round(2 / 12.0, 2));
        snapshot.DataAcquisition.ThroughputBuckets.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Keyword_matching_run_logs_do_not_create_normalization_or_eval_rates()
    {
        var snapshot = await BuildAsync(
            new PipelineSummarySnapshotBuilder.ResolvedDomainData
            {
                Schedule = Schedule("InProgress"),
                AcquisitionSummary = Summary(totalLogs: 1, completed: 1, resources: 8),
                AcquisitionLogs = [CompletedLog(1, start: Minutes(0), durationMs: 8_000, resources: 8)],
                Entries =
                [
                    Entry("p1", "PendingValidation", [
                        new PipelineDataReader.MeasureReportInfo("mr1", "ReadyForValidation", "ACH", [])
                    ])
                ],
                MeasureEvalResourceCounts = [new PipelineDataReader.PatientResourceTypeCount("p1", "Observation", 8)]
            },
            logs:
            [
                "[12:00:00] normalization started",
                "[12:10:00] measure evaluated",
                "[12:20:00] report internal abs manifest validation"
            ]);

        snapshot.Normalization.ActiveDurationSeconds.Should().BeNull();
        snapshot.Normalization.CompletionRatePerSecond.Should().Be(0);
        snapshot.Normalization.AverageResourcesPerSecond.Should().Be(0);
        snapshot.MeasureEval.ActiveDurationSeconds.Should().BeNull();
        snapshot.MeasureEval.CompletionRatePerSecond.Should().Be(0);
        snapshot.MeasureEval.AverageResourcesPerSecond.Should().Be(0);
    }

    [Fact]
    public async Task Normalized_waits_for_data_acquisition_even_if_measure_eval_has_started()
    {
        var snapshot = await BuildAsync(new PipelineSummarySnapshotBuilder.ResolvedDomainData
        {
            Schedule = Schedule("InProgress"),
            AcquisitionSummary = Summary(
                totalLogs: 2,
                completed: 1,
                resources: 4,
                statusCounts:
                [
                    new PipelineDataReader.StatusCountInfo("Completed", 1),
                    new PipelineDataReader.StatusCountInfo("Pending", 1)
                ]),
            Entries =
            [
                Entry("p1", "PendingValidation", [
                    new PipelineDataReader.MeasureReportInfo("mr1", "ReadyForValidation", "ACH", [])
                ])
            ],
            MeasureEvalResourceCounts = [new PipelineDataReader.PatientResourceTypeCount("p1", "Observation", 4)]
        });

        Milestone(snapshot, "Data Acquired").Completed.Should().BeFalse();
        Milestone(snapshot, "Normalized").Completed.Should().BeFalse();
        Milestone(snapshot, "Measure Evaluated").Completed.Should().BeFalse();
    }

    [Fact]
    public async Task Normalized_completes_after_acquisition_once_measure_eval_has_output()
    {
        var snapshot = await BuildAsync(new PipelineSummarySnapshotBuilder.ResolvedDomainData
        {
            Schedule = Schedule("InProgress"),
            AcquisitionSummary = Summary(totalLogs: 1, completed: 1, resources: 4),
            Entries =
            [
                Entry("p1", "PendingValidation", [
                    new PipelineDataReader.MeasureReportInfo("mr1", "ReadyForValidation", "ACH", [])
                ]),
                Entry("p2", "PendingValidation", [])
            ],
            MeasureEvalResourceCounts = [new PipelineDataReader.PatientResourceTypeCount("p1", "Observation", 4)]
        });

        Milestone(snapshot, "Data Acquired").Completed.Should().BeTrue();
        Milestone(snapshot, "Normalized").Completed.Should().BeTrue();
        Milestone(snapshot, "Measure Evaluated").Completed.Should().BeFalse();
    }

    [Fact]
    public async Task Measure_evaluated_requires_every_entry_to_have_a_terminal_measure_report()
    {
        var snapshot = await BuildAsync(new PipelineSummarySnapshotBuilder.ResolvedDomainData
        {
            Schedule = Schedule("InProgress"),
            AcquisitionSummary = Summary(totalLogs: 2, completed: 2, resources: 8),
            Entries =
            [
                Entry("p1", "PendingValidation", [
                    new PipelineDataReader.MeasureReportInfo("mr1", "ReadyForValidation", "ACH", [])
                ]),
                Entry("p2", "NotReportable", [
                    new PipelineDataReader.MeasureReportInfo("mr2", "NotReportable", "ACH", [])
                ])
            ],
            MeasureEvalResourceCounts = [new PipelineDataReader.PatientResourceTypeCount("p1", "Observation", 8)]
        });

        Milestone(snapshot, "Normalized").Completed.Should().BeTrue();
        Milestone(snapshot, "Measure Evaluated").Completed.Should().BeTrue();
    }

    [Fact]
    public async Task Validation_rates_use_passed_and_failed_entry_modify_dates()
    {
        var first = new DateTime(2026, 9, 2, 12, 5, 0, DateTimeKind.Utc);
        var last = first.AddSeconds(20);
        var snapshot = await BuildAsync(new PipelineSummarySnapshotBuilder.ResolvedDomainData
        {
            Schedule = Schedule("Submitted", submittedAt: last.AddSeconds(5)),
            AcquisitionSummary = Summary(totalLogs: 2, completed: 2, resources: 8),
            Entries =
            [
                Entry("p1", "PassedValidation", [
                    new PipelineDataReader.MeasureReportInfo("mr1", "ReadyForValidation", "ACH", [])
                ], created: first.AddMinutes(-2), modified: first),
                Entry("p2", "FailedValidation", [
                    new PipelineDataReader.MeasureReportInfo("mr2", "ReadyForValidation", "ACH", [])
                ], created: first.AddMinutes(-1), modified: last)
            ]
        });

        snapshot.Validation.ActiveDurationSeconds.Should().Be(20);
        snapshot.Validation.CompletionRatePerSecond.Should().Be(0.1);
        snapshot.Validation.AverageResourcesPerSecond.Should().Be(0.1);
        Milestone(snapshot, "Validation").Completed.Should().BeTrue();
    }

    private static async Task<PipelineSummarySnapshotBuilder.PipelineSummarySnapshot> BuildAsync(
        PipelineSummarySnapshotBuilder.ResolvedDomainData data,
        IReadOnlyList<string>? logs = null)
    {
        var builder = new PipelineSummarySnapshotBuilder((_, _) => Task.FromResult(data));
        return await builder.BuildAsync("facility-1", ScheduleId.ToString(), logs ?? []);
    }

    private static PipelineSummarySnapshotBuilder.MilestoneSnapshot Milestone(
        PipelineSummarySnapshotBuilder.PipelineSummarySnapshot snapshot,
        string name)
        => snapshot.Report.Milestones.Should().ContainSingle(m => m.Name == name).Subject;

    private static PipelineDataReader.ReportScheduleInfo Schedule(string status, DateTime? submittedAt = null)
        => new(
            "facility-1",
            status,
            "Adhoc",
            "Census",
            EnableSubmission: false,
            EndOfReportPeriodJobHasRun: false,
            PayloadRootUri: null,
            ReportStartDate: Minutes(0),
            ReportEndDate: Minutes(60),
            CreateDate: Minutes(0),
            SubmitReportDateTime: submittedAt);

    private static PipelineDataReader.AcquisitionSummaryInfo Summary(
        int totalLogs,
        int completed,
        int resources,
        List<PipelineDataReader.StatusCountInfo>? statusCounts = null)
        => new(
            ScheduleId.ToString(),
            totalLogs,
            totalLogs,
            completed,
            resources,
            TotalRetryAttempts: 0,
            TotalCompletionTimeMs: 0,
            AverageCompletionTimeMs: 0,
            statusCounts ?? [new PipelineDataReader.StatusCountInfo("Completed", completed)],
            []);

    private static PipelineDataReader.AcquisitionLogInfo CompletedLog(long id, DateTime start, long durationMs, int resources)
        => new(
            id,
            $"p{id}",
            null,
            ScheduleId.ToString(),
            "Completed",
            "Initial",
            [],
            Enumerable.Range(0, resources).Select(i => $"Observation/{id}-{i}").ToList(),
            [],
            ExecutionDate: start,
            CreateDate: start,
            CompletionDate: start.AddMilliseconds(durationMs),
            CompletionTimeMilliseconds: durationMs);

    private static PipelineDataReader.ReportEntryInfo Entry(
        string patientId,
        string reportingStatus,
        List<PipelineDataReader.MeasureReportInfo> measureReports,
        DateTime? created = null,
        DateTime? modified = null)
        => new(
            Guid.NewGuid(),
            "facility-1",
            patientId,
            reportingStatus,
            "PendingValidation",
            measureReports,
            created,
            modified);

    private static DateTime Minutes(double minutes)
        => new DateTime(2026, 9, 2, 12, 0, 0, DateTimeKind.Utc).AddMinutes(minutes);
}
