using System.Text.Json;
using Automation.UI.Models;
using Automation.UI.Services;
using FluentAssertions;
using LantanaGroup.Link.Automation.Link.Helpers;

namespace UnitTests.AutomationUI;

[Trait("Category", "UnitTests")]
public class RunHistorySlimTests
{
    [Fact]
    public void Acquisition_logs_keep_timing_and_drop_notes_ids_and_queries()
    {
        var start = new DateTime(2026, 10, 5, 12, 0, 0, DateTimeKind.Utc);
        var slim = RunHistorySlim.SlimAcquisitionLogs(
        [
            new PipelineDataReader.AcquisitionLogInfo(
                7,
                "patient-1",
                "correlation",
                "report",
                "Completed",
                "Initial",
                ["a long note"],
                ["Patient/patient-1", "Observation/1"],
                [new PipelineDataReader.FhirQueryInfo(["Observation"])],
                ExecutionDate: start,
                CreateDate: start,
                CompletionDate: start.AddSeconds(4),
                CompletionTimeMilliseconds: 4_000,
                ResourceTypes: ["Observation"])
        ]);

        slim.Should().ContainSingle();
        slim[0].Id.Should().Be(7);
        slim[0].PatientId.Should().Be("patient-1");
        slim[0].Status.Should().Be("Completed");
        slim[0].CompletionTimeMilliseconds.Should().Be(4_000);
        slim[0].ResourceTypes.Should().Equal("Observation");
        slim[0].Notes.Should().BeEmpty();
        slim[0].ResourceAcquiredIds.Should().BeEmpty();
        slim[0].FhirQueries.Should().BeEmpty();
    }

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
        slim.SuiteSequences.Should().ContainSingle();
        slim.OperationConfigs.Should().ContainSingle();
        slim.OperationConfigs[0].Name.Should().Be("Copy");
        slim.OperationConfigs[0].Conditions.Should().BeEmpty();
        slim.OperationConfigs[0].CodeSystemMaps.Should().BeEmpty();
    }

    [Fact]
    public void Stored_acquisition_logs_drop_notes_ids_and_queries()
    {
        var slim = RunHistorySlim.TrySlimStoredJson(
            "acquisitionLogs",
            """[{"Id":7,"PatientId":"p1","Status":"Completed","QueryPhase":"Initial","Notes":["secret"],"ResourceAcquiredIds":["Patient/p1"],"FhirQueries":[{"ResourceTypes":["Observation"]}],"ResourceTypes":["Observation"]}]""");

        slim.Should().NotBeNull();
        var logs = JsonSerializer.Deserialize<List<PipelineDataReader.AcquisitionLogInfo>>(slim!);
        logs.Should().ContainSingle();
        logs![0].Notes.Should().BeEmpty();
        logs[0].ResourceAcquiredIds.Should().BeEmpty();
        logs[0].FhirQueries.Should().BeEmpty();
        logs[0].ResourceTypes.Should().Equal("Observation");
        RunHistorySlim.TrySlimStoredJson("acquisitionLogs", slim).Should().BeNull();
    }

    [Fact]
    public void Stored_population_id_list_becomes_counts()
    {
        var slim = RunHistorySlim.TrySlimStoredJson(
            "populations",
            """[{"ReportType":"ACH","GroupPopulations":[{"PopulationCodeJson":"{}","MeasureReportPopulations":[{"MeasureReportId":"mr-1"}]}]}]""");

        var counts = JsonSerializer.Deserialize<PipelineDataReader.PopulationCountSnapshot>(slim!);
        counts!.ReportTypeCount.Should().Be(1);
        counts.GroupCount.Should().Be(1);
        counts.MeasureReportPopulationCount.Should().Be(1);
        RunHistorySlim.TrySlimStoredJson("populations", slim).Should().BeNull();
    }

    [Fact]
    public void Stored_item_array_on_a_populations_domain_is_left_alone()
    {
        RunHistorySlim.TrySlimStoredJson("populations", """[{"Id":"1","Note":"x"}]""").Should().BeNull();
    }

    [Fact]
    public void Stored_measure_rows_roll_up_and_aggregated_rows_stay()
    {
        var slim = RunHistorySlim.TrySlimStoredJson(
            "measureResources",
            """[{"PatientId":"p1","ResourceType":"Observation","Count":2},{"PatientId":"p2","ResourceType":"Observation","Count":3}]""");

        var rows = JsonSerializer.Deserialize<List<PipelineDataReader.PatientResourceTypeCount>>(slim!);
        rows.Should().ContainSingle();
        rows![0].PatientId.Should().BeEmpty();
        rows[0].Count.Should().Be(5);
        RunHistorySlim.TrySlimStoredJson("measureResources", slim).Should().BeNull();
    }

    [Fact]
    public void Stored_org_location_rows_become_counts()
    {
        var slim = RunHistorySlim.TrySlimStoredJson(
            "orgLocation",
            """{"Configurations":[{"ConfigId":1,"IsActive":true,"ConditionsCount":0}],"LocationMappings":[],"EncounterMappings":[{"FacilityId":"f","PatientId":"p","EncounterId":"e","MappedToOrg":false,"EncounterLocations":[]}]}""");

        var summary = JsonSerializer.Deserialize<OrgLocationSummary>(slim!);
        summary!.ConfigurationCount.Should().Be(1);
        summary.ActiveConfigurationCount.Should().Be(1);
        summary.EncounterMappingCount.Should().Be(1);
        summary.MappedToOrgCount.Should().Be(0);
        RunHistorySlim.TrySlimStoredJson("orgLocation", slim).Should().BeNull();
    }

    [Fact]
    public void Stored_normalization_lines_are_dropped_and_a_summary_stays()
    {
        var slim = RunHistorySlim.TrySlimStoredJson(
            "normalizationEvidence",
            """{"SuiteName":"Epic","CollectedLineCount":4,"EvidenceChunkCount":1,"SummaryLines":["raw"],"ParsedSteps":[{"ResourceId":"Patient/1"}],"OperationConfigs":[{"Name":"Copy","OperationType":"CopyLocation","ResourceTypes":["Encounter"],"Conditions":["long"]}]}""");

        var evidence = JsonSerializer.Deserialize<NormalizationEvidenceSnapshot>(slim!);
        evidence!.SuiteName.Should().Be("Epic");
        evidence.CollectedLineCount.Should().Be(4);
        evidence.EvidenceChunkCount.Should().Be(0);
        evidence.StepsCollapsed.Should().BeFalse();
        evidence.SummaryLines.Should().BeEmpty();
        evidence.ParsedSteps.Should().BeEmpty();
        evidence.OperationConfigs.Should().ContainSingle();
        evidence.OperationConfigs[0].Conditions.Should().BeEmpty();
        RunHistorySlim.TrySlimStoredJson("normalizationEvidence", slim).Should().BeNull();
        RunHistorySlim.IsNormalizationEvidenceChunkDomain("normalizationEvidence-chunk-2").Should().BeTrue();
        RunHistorySlim.IsNormalizationEvidenceChunkDomain("normalizationEvidence").Should().BeFalse();
    }
}
