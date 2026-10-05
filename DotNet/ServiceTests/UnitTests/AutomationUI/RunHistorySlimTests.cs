using Automation.UI.Models;
using Automation.UI.Services;
using FluentAssertions;
using LantanaGroup.Link.Automation.Link.Helpers;

namespace UnitTests.AutomationUI;

[Trait("Category", "UnitTests")]
public class RunHistorySlimTests
{
    [Fact]
    public void Acquisition_chart_keeps_durations_buckets_and_drops_patient_rows()
    {
        var start = new DateTime(2026, 10, 5, 12, 0, 0, DateTimeKind.Utc);
        var chart = RunHistorySlim.ToAcquisitionChart(
        [
            Log(7, "patient-1", "Completed", "Initial", start, 1_000, ["Observation"], ["a long note"]),
            Log(8, "patient-2", "Completed", "Initial", start.AddSeconds(10), 3_000, ["Observation"]),
            Log(9, "patient-3", "Completed", "Initial", start.AddSeconds(10), 5_000, ["Encounter"])
        ]);

        chart.TotalLogs.Should().Be(3);
        chart.CompletedCount.Should().Be(3);
        chart.MinDurationMs.Should().Be(1_000);
        chart.AverageDurationMs.Should().Be(3_000);
        chart.MaxDurationMs.Should().Be(5_000);
        chart.ThroughputBuckets.Select(bucket => (bucket.Label, bucket.Count)).Should().Equal(("0s", 1), ("10s", 2));
        chart.ResourceTypeCounts.Select(type => (type.Status, type.Count)).Should().Equal(("Observation", 2), ("Encounter", 1));
        var json = System.Text.Json.JsonSerializer.Serialize(chart);
        json.Should().NotContain("patient-1");
        json.Should().NotContain("a long note");
    }

    [Fact]
    public void Acquisition_chart_counts_resource_types_that_were_only_on_the_query()
    {
        var chart = RunHistorySlim.ToAcquisitionChart(
        [
            new PipelineDataReader.AcquisitionLogInfo(
                8,
                "patient-2",
                null,
                null,
                "Completed",
                "Initial",
                [],
                [],
                [
                    new PipelineDataReader.FhirQueryInfo(["Patient"]),
                    new PipelineDataReader.FhirQueryInfo(["observation"])
                ],
                ResourceTypes: ["Observation"])
        ]);

        chart.ResourceTypeCounts.Select(type => type.Status).Should().Equal("Observation", "Patient");
    }

    [Fact]
    public void Acquisition_chart_caps_the_failure_sample_and_stays_small_for_10_000_logs()
    {
        var start = new DateTime(2026, 10, 5, 12, 0, 0, DateTimeKind.Utc);
        var logs = new List<PipelineDataReader.AcquisitionLogInfo>(10_000);
        for (var i = 0; i < 10_000; i++)
        {
            var status = i < 25 ? "Failed" : "Completed";
            logs.Add(Log(i + 1, $"patient-{i}", status, "Initial", start.AddSeconds(i % 30), 100, ["Observation"], ["boom " + i]));
        }

        var chart = RunHistorySlim.ToAcquisitionChart(logs);
        chart.TotalLogs.Should().Be(10_000);
        chart.FailureCount.Should().Be(25);
        chart.Failures.Should().HaveCount(RunHistorySlim.AcquisitionFailureSampleCap);
        chart.Failures.Should().OnlyContain(failure => failure.LogId > 0 && failure.Message != null && failure.Message.StartsWith("boom"));
        var json = System.Text.Json.JsonSerializer.Serialize(chart);
        json.Should().NotContain("patient-");
        System.Text.Encoding.UTF8.GetByteCount(json).Should().BeLessThan(20_000);
    }

    [Fact]
    public void Acquisition_chart_counts_MaxRetriesReached_and_apply_keeps_the_sample_without_timestamps()
    {
        var chart = RunHistorySlim.ToAcquisitionChart(
        [
            new PipelineDataReader.AcquisitionLogInfo(
                4,
                "patient-9",
                null,
                null,
                "MaxRetriesReached",
                "Initial",
                ["gave up"],
                [],
                [],
                ResourceTypes: ["Observation"])
        ]);

        chart.FailureCount.Should().Be(1);
        chart.SpanCount.Should().Be(0);
        chart.CompletedCount.Should().Be(0);
        chart.Failures.Should().ContainSingle();

        var target = new PipelineSummarySnapshotBuilder.DataAcquisitionSnapshot();
        chart.Apply(target, 0, DateTimeOffset.UtcNow);
        target.Errors.Should().ContainSingle().Which.Should().Contain("MaxRetriesReached").And.Contain("gave up");
        target.ActiveDurationSeconds.Should().BeNull();
        target.ThroughputBuckets.Should().BeEmpty();

        var alreadyReported = new PipelineSummarySnapshotBuilder.DataAcquisitionSnapshot { Errors = ["loki"] };
        chart.Apply(alreadyReported, 0, DateTimeOffset.UtcNow);
        alreadyReported.Errors.Should().Equal("loki");
    }

    private static PipelineDataReader.AcquisitionLogInfo Log(
        long id,
        string patientId,
        string status,
        string phase,
        DateTime completed,
        long durationMs,
        IEnumerable<string> types,
        IEnumerable<string>? notes = null)
        => new(
            id,
            patientId,
            "correlation",
            "report",
            status,
            phase,
            notes?.ToList() ?? [],
            ["Observation/1"],
            [new PipelineDataReader.FhirQueryInfo(types.ToList())],
            ExecutionDate: completed.AddMilliseconds(-durationMs),
            CreateDate: completed.AddMilliseconds(-durationMs),
            CompletionDate: completed,
            CompletionTimeMilliseconds: durationMs,
            ResourceTypes: types.ToList());

    [Fact]
    public void Populations_keep_counts_and_drop_measure_report_ids()
    {
        var counts = RunHistorySlim.ToPopulationCounts(
        [
            new PipelineDataReader.ReportPopulationInfo(
                "ACH",
                [
                    new PipelineDataReader.GroupPopulationInfo(
                        "{\"code\":\"initial-population\"}",
                        [
                            new PipelineDataReader.MeasureReportPopulationInfo("mr-1"),
                            new PipelineDataReader.MeasureReportPopulationInfo("mr-2")
                        ])
                ])
        ]);

        counts.ReportTypeCount.Should().Be(1);
        counts.GroupCount.Should().Be(1);
        counts.MeasureReportPopulationCount.Should().Be(2);
    }

    [Fact]
    public void Measure_resources_roll_up_by_type()
    {
        var rows = RunHistorySlim.SlimMeasureResources(
        [
            new PipelineDataReader.PatientResourceTypeCount("p1", "Observation", 3),
            new PipelineDataReader.PatientResourceTypeCount("p2", "observation", 4),
            new PipelineDataReader.PatientResourceTypeCount("p2", "Encounter", 1)
        ]);

        rows.Should().Equal(
            new PipelineDataReader.PatientResourceTypeCount("", "Observation", 7),
            new PipelineDataReader.PatientResourceTypeCount("", "Encounter", 1));
    }

    [Fact]
    public void Org_location_keeps_totals_only()
    {
        var summary = RunHistorySlim.SlimOrgLocation(new StoreBackedServicePoller.OrgLocationSnapshot(
            [new PipelineDataReader.OrganizationLocationConfigurationInfo(1, true, 2)],
            [
                new PipelineDataReader.OrganizationLocationMappingInfo("f", "loc-1", true, true, null),
                new PipelineDataReader.OrganizationLocationMappingInfo("f", "loc-2", false, true, "loc-1")
            ],
            [
                new PipelineDataReader.EncounterMappingInfo(
                    "f", "p1", "enc-1", false, [new PipelineDataReader.EncounterLocationInfo("loc-2")])
            ]));

        summary.ConfigurationCount.Should().Be(1);
        summary.ActiveConfigurationCount.Should().Be(1);
        summary.LocationMappingCount.Should().Be(2);
        summary.OrgLocationMappingCount.Should().Be(1);
        summary.EncounterMappingCount.Should().Be(1);
        summary.MappedToOrgCount.Should().Be(0);
    }

    [Fact]
    public void Normalization_evidence_drops_lines_steps_and_operation_blobs()
    {
        var slim = RunHistorySlim.SlimNormalizationEvidence(new NormalizationEvidenceSnapshot
        {
            SuiteName = "Epic",
            CollectedLineCount = 40,
            SummaryLines = ["raw line"],
            ParsedSteps = [new NormalizationEvidenceStep { ResourceId = "Patient/1", OperationName = "Copy" }],
            OperationConfigs =
            [
                new NormalizationOperationConfigSnapshot
                {
                    Name = "Copy",
                    OperationType = "CopyLocation",
                    ResourceTypes = ["Encounter"],
                    Conditions = ["a long condition"],
                    CodeSystemMaps = ["system|code"]
                }
            ],
            SuiteSequences =
            [
                new NormalizationSuiteSequenceStep { SequenceName = "main", Sequence = 1, OperationName = "Copy" }
            ]
        });

        slim.SuiteName.Should().Be("Epic");
        slim.CollectedLineCount.Should().Be(40);
        slim.RawLinesOmitted.Should().BeTrue();
        slim.StepsCollapsed.Should().BeFalse();
        slim.EvidenceChunkCount.Should().Be(0);
        slim.SummaryLines.Should().BeEmpty();
        slim.ParsedSteps.Should().BeEmpty();
        slim.OmittedStepCount.Should().Be(1);
        slim.SuiteSequences.Should().ContainSingle();
        var export = NormalizationDiagnosticsWriter.FormatExportAppendix(slim);
        export.Should().Contain("1 execution step(s) omitted from this snapshot");
        export.Should().NotContain("no parsable [NormalizationExecutionSummary] steps");
        slim.OperationConfigs.Should().ContainSingle();
        slim.OperationConfigs[0].Name.Should().Be("Copy");
        slim.OperationConfigs[0].Conditions.Should().BeEmpty();
        slim.OperationConfigs[0].CodeSystemMaps.Should().BeEmpty();
    }
}

