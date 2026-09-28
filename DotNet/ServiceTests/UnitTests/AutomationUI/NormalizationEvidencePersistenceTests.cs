using Automation.UI.Models;
using Automation.UI.Services;
using Automation.UI.Services.Persistence;
using FluentAssertions;

namespace UnitTests.AutomationUI;

[Trait("Category", "UnitTests")]
public class NormalizationEvidencePersistenceTests
{
    [Fact]
    public void Normalization_evidence_is_externalized_when_it_outgrows_an_inline_document()
    {
        var settings = new ImportedBundleBlobStorageSettings();

        settings.SnapshotPayloadExternalizedDomains.Should().Contain(NormalizationEvidenceSnapshot.Domain);
    }

    [Fact]
    public void Oversized_snapshot_drops_raw_lines_and_keeps_the_parsed_steps()
    {
        var snapshot = new NormalizationEvidenceSnapshot
        {
            SuiteName = "System Default",
            CollectedLineCount = 2,
            SummaryLines = [new string('x', 8_000), new string('y', 8_000)],
            ParsedSteps =
            [
                Step("Observation", "obs-1", 1, "Success"),
                Step("Observation", "obs-2", 1, "Success")
            ]
        };

        var fitted = NormalizationDiagnosticsWriter.FitToCosmosInlineLimit(snapshot, maxUtf8Bytes: 4_000);

        fitted.Should().NotBeSameAs(snapshot);
        fitted.RawLinesOmitted.Should().BeTrue();
        fitted.SummaryLines.Should().BeEmpty();
        fitted.CollectedLineCount.Should().Be(2);
        fitted.StepsCollapsed.Should().BeFalse();
        fitted.ParsedSteps.Should().HaveCount(2);
        NormalizationDiagnosticsWriter.SerializedUtf8Bytes(fitted).Should().BeLessThanOrEqualTo(4_000);
    }

    [Fact]
    public void Still_oversized_snapshot_rolls_steps_up_and_the_inventory_keeps_the_count()
    {
        var steps = Enumerable.Range(0, 40)
            .Select(i => Step("Observation", $"obs-{i}", 1, "Success"))
            .ToList();
        var snapshot = new NormalizationEvidenceSnapshot
        {
            SuiteName = "System Default",
            CollectedLineCount = steps.Count,
            SummaryLines = [new string('x', 4_000)],
            ParsedSteps = steps
        };

        var fitted = NormalizationDiagnosticsWriter.FitToCosmosInlineLimit(snapshot, maxUtf8Bytes: 900);

        fitted.RawLinesOmitted.Should().BeTrue();
        fitted.StepsCollapsed.Should().BeTrue();
        fitted.ParsedSteps.Should().ContainSingle();
        fitted.ParsedSteps[0].Count.Should().Be(40);
        NormalizationDiagnosticsWriter.SerializedUtf8Bytes(fitted).Should().BeLessThanOrEqualTo(900);

        var inventory = NormalizationDiagnosticsWriter.FormatEvidenceInventory(fitted.ParsedSteps);
        inventory.Should().ContainSingle().Which.Should().Contain("Success=40");
    }

    [Fact]
    public void Cosmos_413_is_an_oversized_write()
    {
        var ex = new InvalidOperationException(
            "Response status code does not indicate success: RequestEntityTooLarge (413); Reason: Request size is too large");

        NormalizationDiagnosticsWriter.IsOversizedWrite(ex).Should().BeTrue();
        NormalizationDiagnosticsWriter.IsOversizedWrite(new InvalidOperationException("connection reset")).Should().BeFalse();
    }

    private static NormalizationEvidenceStep Step(string resourceType, string resourceId, int sequence, string outcome)
        => new()
        {
            ResourceType = resourceType,
            ResourceId = resourceId,
            Sequence = sequence,
            OperationType = "RemoveExtensions",
            OperationName = "Remove Common Extensions",
            Outcome = outcome
        };
}
