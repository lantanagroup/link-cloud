using Automation.UI.Models;
using Automation.UI.Services;
using FluentAssertions;

namespace UnitTests.AutomationUI;

[Trait("Category", "UnitTests")]
public class NormalizationDiagnosticsWriterTests
{
    [Fact]
    public void FormatExportAppendix_includes_suite_vs_runtime_sequences_and_raw_summaries()
    {
        var copyLocation = new NormalizationOperationDefinition
        {
            Id = Guid.NewGuid(),
            Name = "Copy Location Identifiers to Type",
            OperationType = "CopyLocation",
            ResourceTypes = ["Location"]
        };
        var copyAlias = new NormalizationOperationDefinition
        {
            Id = Guid.NewGuid(),
            Name = "Copy Location aliases to type iteratively",
            OperationType = "CopyLocationAliasToTypeIteratively",
            ResourceTypes = ["Location"]
        };
        var transform = new NormalizationOperationDefinition
        {
            Id = Guid.NewGuid(),
            Name = "Set Encounter status when class matches upload",
            OperationType = "ConditionalTransform",
            ResourceTypes = ["Encounter"],
            ConditionTargetFhirPath = "status",
            ConditionTargetValue = "in-progress",
            Conditions = [new NormalizationCondition { FhirPathSource = "class.code", Operator = "Equal", Value = "IMP" }]
        };

        var suite = new NormalizationSuiteResolution(
            "System Default extended",
            [copyLocation, copyAlias, transform],
            [
                new NormalizationSuiteSequenceResolution(
                    "Default Location Normalization",
                    [new NormalizationSuiteSequenceOperationResolution(1, copyLocation)]),
                new NormalizationSuiteSequenceResolution(
                    "Generated patient sequence",
                    [
                        new NormalizationSuiteSequenceOperationResolution(1, copyAlias),
                        new NormalizationSuiteSequenceOperationResolution(2, transform)
                    ])
            ],
            []);

        var runtime = new List<NormalizationRuntimeSequenceStep>
        {
            new() { ResourceType = "Location", Sequence = 1, OperationType = "CopyLocation", OperationName = copyLocation.Name },
            new() { ResourceType = "Location", Sequence = 2, OperationType = "CopyLocationAliasToTypeIteratively", OperationName = copyAlias.Name },
            new() { ResourceType = "Encounter", Sequence = 1, OperationType = "ConditionalTransform", OperationName = transform.Name }
        };

        var logs = new List<string>
        {
            "[NormalizationExecutionSummary] FacilityId=f1, PatientId=p1, CorrelationId=c1, ReportTrackingId=r1, ResourceType=Location, ResourceId=loc-1, Steps=[1:CopyLocation:Copy Location Identifiers to Type:Success | 2:CopyLocationAliasToTypeIteratively:Copy Location aliases to type iteratively:Success]"
        };

        var snapshot = NormalizationDiagnosticsWriter.Build(suite, runtime, logs);
        var export = NormalizationDiagnosticsWriter.FormatExportAppendix(snapshot);

        export.Should().Contain("Suite 'System Default extended' sequences");
        export.Should().Contain("Generated patient sequence");
        export.Should().Contain("Sequence=1 CopyLocationAliasToTypeIteratively");
        export.Should().Contain("Runtime sequences (Normalization service, per resource type)");
        export.Should().Contain("Location");
        export.Should().Contain("Sequence=2 CopyLocationAliasToTypeIteratively");
        export.Should().Contain("When class.code Equal IMP");
        export.Should().Contain("Set status = in-progress");
        export.Should().Contain("Location#2 CopyLocationAliasToTypeIteratively");
        export.Should().Contain("Raw [NormalizationExecutionSummary] lines (1)");
        export.Should().Contain(logs[0]);
    }

    [Fact]
    public void FormatExportAppendix_notes_when_snapshot_is_missing()
    {
        var export = NormalizationDiagnosticsWriter.FormatExportAppendix(null);
        export.Should().Contain("not persisted for this run");
    }
}
