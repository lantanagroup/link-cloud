using Automation.UI.Models;
using Automation.UI.Services;
using Automation.UI.Services.Persistence;
using FluentAssertions;

namespace UnitTests.AutomationUI;

[Trait("Category", "UnitTests")]
public class NormalizationEvidencePersistenceTests
{
    [Fact]
    public void Normalization_evidence_stays_in_the_snapshot_store()
    {
        var settings = new ImportedBundleBlobStorageSettings();

        settings.SnapshotPayloadExternalizedDomains.Should().NotContain(NormalizationEvidenceSnapshot.Domain);
    }

    [Fact]
    public void Snapshot_under_the_cap_is_one_document()
    {
        var snapshot = new NormalizationEvidenceSnapshot
        {
            SuiteName = "System Default",
            CollectedLineCount = 1,
            SummaryLines = ["one line"],
            ParsedSteps = [Step("Observation", "obs-1", 1, "Success")]
        };

        var plan = NormalizationDiagnosticsWriter.PlanPersistence(snapshot, maxUtf8Bytes: 50_000);

        plan.Chunks.Should().BeEmpty();
        plan.Header.EvidenceChunkCount.Should().Be(0);
        plan.Header.SummaryLines.Should().Equal(snapshot.SummaryLines);
        plan.Header.ParsedSteps.Should().HaveCount(1);
    }

    [Fact]
    public void Oversized_evidence_is_chunked_and_reassembles_without_losing_counts()
    {
        var lines = new[] { new string('x', 3_000), new string('y', 3_000), new string('z', 3_000) };
        var steps = Enumerable.Range(0, 30)
            .Select(i => Step("Observation", $"obs-{i}", 1, "Success"))
            .ToList();
        var snapshot = new NormalizationEvidenceSnapshot
        {
            SuiteName = "System Default",
            CollectedLineCount = lines.Length,
            SummaryLines = [.. lines],
            ParsedSteps = steps
        };

        var plan = NormalizationDiagnosticsWriter.PlanPersistence(snapshot, maxUtf8Bytes: 4_000);

        plan.Chunks.Count.Should().BeGreaterThan(1);
        plan.Header.EvidenceChunkCount.Should().Be(plan.Chunks.Count);
        plan.Header.SummaryLines.Should().BeEmpty();
        plan.Header.RawLinesOmitted.Should().BeFalse();
        plan.Header.StepsCollapsed.Should().BeTrue();
        plan.Header.ParsedSteps.Should().ContainSingle();
        plan.Header.ParsedSteps[0].Count.Should().Be(30);
        plan.Header.CollectedLineCount.Should().Be(lines.Length);
        NormalizationDiagnosticsWriter.SerializedUtf8Bytes(plan.Header).Should().BeLessThanOrEqualTo(4_000);
        foreach (var chunk in plan.Chunks)
        {
            var bytes = System.Text.Encoding.UTF8.GetByteCount(System.Text.Json.JsonSerializer.Serialize(chunk));
            bytes.Should().BeLessThanOrEqualTo(4_000);
        }

        var assembled = NormalizationDiagnosticsWriter.Assemble(plan.Header, plan.Chunks);
        assembled.SummaryLines.Should().Equal(lines);
        assembled.ParsedSteps.Should().HaveCount(30);
        assembled.StepsCollapsed.Should().BeFalse();

        var partial = NormalizationDiagnosticsWriter.Assemble(plan.Header, plan.Chunks.Take(1).ToList());
        partial.ParsedSteps.Should().ContainSingle();
        partial.ParsedSteps[0].Count.Should().Be(30);
        partial.StepsCollapsed.Should().BeTrue();
    }

    [Fact]
    public void Export_distinguishes_omitted_and_chunked_lines_from_an_empty_scrape()
    {
        var omitted = NormalizationDiagnosticsWriter.FormatExportAppendix(new NormalizationEvidenceSnapshot
        {
            CollectedLineCount = 12,
            RawLinesOmitted = true
        });
        omitted.Should().Contain("12 collected; raw lines omitted");
        omitted.Should().NotContain("(none collected)");

        var chunked = NormalizationDiagnosticsWriter.FormatExportAppendix(new NormalizationEvidenceSnapshot
        {
            CollectedLineCount = 4,
            EvidenceChunkCount = 3
        });
        chunked.Should().Contain("raw lines are stored in 3 snapshot chunk(s)");
        chunked.Should().NotContain("(none collected)");

        var empty = NormalizationDiagnosticsWriter.FormatExportAppendix(new NormalizationEvidenceSnapshot());
        empty.Should().Contain("(none collected)");
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
