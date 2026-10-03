using System.Text;
using System.Text.Json;
using Automation.UI.Services.Persistence;
using FluentAssertions;

// ServiceTests globally imports Hl7.Fhir.Model, which has its own Task type.
using Task = System.Threading.Tasks.Task;

namespace UnitTests.AutomationUI;

[Trait("Category", "UnitTests")]
public class SnapshotPartitionerTests
{
    [Fact]
    public void Empty_and_small_payloads_stay_one_document()
    {
        SnapshotPartitioner.Plan("[]").Should().BeOfType<SnapshotPlan.Inline>();
        SnapshotPartitioner.Plan("{}").Should().BeOfType<SnapshotPlan.Inline>();
        SnapshotPartitioner.Plan("null").Should().BeOfType<SnapshotPlan.Inline>();
        SnapshotPartitioner.Plan("\"\"").Should().BeOfType<SnapshotPlan.Inline>();
    }

    [Fact]
    public void Payload_exactly_at_the_budget_stays_inline_and_one_byte_over_is_split()
    {
        var exactBody = new string('a', 1000);
        var exact = JsonSerializer.Serialize(exactBody);
        var budget = SnapshotPartitioner.EstimateStoredDocumentBytes(exact);
        var under = JsonSerializer.Serialize(new string('a', 999));
        var over = JsonSerializer.Serialize(new string('a', 1001));

        SnapshotPartitioner.EstimateStoredDocumentBytes(under).Should().BeLessThan(budget);
        SnapshotPartitioner.EstimateStoredDocumentBytes(over).Should().BeGreaterThan(budget);
        SnapshotPartitioner.Plan(exact, budget).Should().BeOfType<SnapshotPlan.Inline>();
        SnapshotPartitioner.Plan(under, budget).Should().BeOfType<SnapshotPlan.Inline>();

        var split = SnapshotPartitioner.Plan(over, budget).Should().BeOfType<SnapshotPlan.Partitioned>().Subject;
        AssertEveryPieceFits(split, budget);
        RoundTrip(split).Should().Be(over);
    }

    [Fact]
    public void Unicode_slices_do_not_split_a_character()
    {
        var text = string.Concat(Enumerable.Repeat("é😀", 40));
        var json = JsonSerializer.Serialize(text);
        var budget = SnapshotPartitioner.DocumentEnvelopeBytes + 2 + 12;

        var split = SnapshotPartitioner.Plan(json, budget).Should().BeOfType<SnapshotPlan.Partitioned>().Subject;
        split.Pieces.Count.Should().BeGreaterThan(1);
        AssertEveryPieceFits(split, budget);
        RoundTrip(split).Should().Be(json);
        JsonSerializer.Deserialize<string>(RoundTrip(split)).Should().Be(text);
    }

    [Fact]
    public void Array_of_records_is_one_document_per_record()
    {
        var items = Enumerable.Range(0, 12)
            .Select(i => new Record(i, new string('n', 180)))
            .ToList();
        var json = JsonSerializer.Serialize(items);
        var budget = 4_000;

        var split = SnapshotPartitioner.Plan(json, budget).Should().BeOfType<SnapshotPlan.Partitioned>().Subject;
        split.Pieces.Should().OnlyContain(p => p.Kind == "element");
        split.Pieces.Select(p => p.ItemKey).Should().Equal(items.Select(i => i.Id.ToString()));
        AssertEveryPieceFits(split, budget);

        JsonSerializer.Deserialize<List<Record>>(RoundTrip(split)).Should().Equal(items);
    }

    [Fact]
    public void Many_tiny_values_are_packed_instead_of_one_document_each()
    {
        var values = Enumerable.Repeat("ab", 2_000).ToList();
        var json = JsonSerializer.Serialize(values);
        var budget = 8_000;

        var split = SnapshotPartitioner.Plan(json, budget).Should().BeOfType<SnapshotPlan.Partitioned>().Subject;
        split.Pieces.Should().OnlyContain(p => p.Kind == "pack");
        split.Pieces.Count.Should().BeLessThan(values.Count);
        AssertEveryPieceFits(split, budget);
        JsonSerializer.Deserialize<List<string>>(RoundTrip(split)).Should().Equal(values);
    }

    [Fact]
    public void Object_keeps_small_properties_and_splits_the_large_collection()
    {
        var payload = new Wrapped(
            "stay",
            Enumerable.Range(0, 20).Select(i => new Record(i, new string('q', 180))).ToList());
        var json = JsonSerializer.Serialize(payload);
        var budget = 4_000;

        var split = SnapshotPartitioner.Plan(json, budget).Should().BeOfType<SnapshotPlan.Partitioned>().Subject;
        split.SkeletonJson.Should().Contain("stay");
        AssertEveryPieceFits(split, budget);
        JsonSerializer.Deserialize<Wrapped>(RoundTrip(split)).Should().BeEquivalentTo(payload);
    }

    [Fact]
    public void Per_patient_map_round_trips()
    {
        var counts = new Dictionary<string, Dictionary<string, int>>();
        for (var i = 0; i < 40; i++)
            counts[$"patient-{i}"] = new Dictionary<string, int> { ["Encounter"] = i, ["Condition"] = i + 1 };

        var payload = new Manifest(40, counts);
        var json = JsonSerializer.Serialize(payload);
        var budget = 3_500;

        var split = SnapshotPartitioner.Plan(json, budget).Should().BeOfType<SnapshotPlan.Partitioned>().Subject;
        AssertEveryPieceFits(split, budget);
        var rebuilt = JsonSerializer.Deserialize<Manifest>(RoundTrip(split));
        rebuilt!.PatientCount.Should().Be(40);
        rebuilt.ResourceCountsByPatient.Should().BeEquivalentTo(counts);
    }

    [Fact]
    public void One_value_larger_than_the_cap_is_split_without_dropping_bytes()
    {
        var payload = new NoteHolder(new string('z', 20_000));
        var json = JsonSerializer.Serialize(payload);
        var budget = 4_096;

        var split = SnapshotPartitioner.Plan(json, budget).Should().BeOfType<SnapshotPlan.Partitioned>().Subject;
        split.Pieces.Should().Contain(p => p.Kind == "slice");
        AssertEveryPieceFits(split, budget);
        JsonSerializer.Deserialize<NoteHolder>(RoundTrip(split))!.Note.Should().Be(payload.Note);
    }

    [Fact]
    public void Budget_smaller_than_the_envelope_is_rejected()
    {
        var act = () => SnapshotPartitioner.Plan("{}", SnapshotPartitioner.DocumentEnvelopeBytes);
        act.Should().Throw<SnapshotDocumentTooLargeException>();
    }

    [Fact]
    public void Hard_cap_guard_rejects_a_document_over_two_megabytes()
    {
        var over = new string('a', SnapshotPartitioner.HardCapBytes);
        var act = () => SnapshotPartitioner.EnsureWithinHardCap(over);
        act.Should().Throw<SnapshotDocumentTooLargeException>();

        var under = new string('a', 1024);
        var read = () => SnapshotPartitioner.EnsureWithinHardCap(under);
        read.Should().NotThrow();
    }

    [Fact]
    public void Reader_returns_null_when_the_committed_generation_is_incomplete()
    {
        var payload = Enumerable.Range(0, 12)
            .Select(i => new Record(i, new string('n', 180)))
            .ToList();
        var json = JsonSerializer.Serialize(payload);
        var split = (SnapshotPlan.Partitioned)SnapshotPartitioner.Plan(json, 4_000);
        var header = SnapshotPartitioner.BuildHeaderJson("gen-a", split.Mode, split.Pieces.Count, split.SkeletonJson);

        SnapshotPartitioner.ReadCommitted(header, split.Pieces).Should().NotBeNull();
        SnapshotPartitioner.ReadCommitted(header, split.Pieces.Take(split.Pieces.Count - 1).ToList()).Should().BeNull();
        SnapshotPartitioner.ReadCommitted("{\"Name\":\"inline\"}", []).Should().Be("{\"Name\":\"inline\"}");
    }

    [Fact]
    public void Translation_skips_documents_that_already_fit_or_are_already_partitioned()
    {
        SnapshotPartitioner.ShouldTranslateStoredData("{}").Should().BeFalse();
        SnapshotPartitioner.ShouldTranslateStoredData(null).Should().BeFalse();

        var header = SnapshotPartitioner.BuildHeaderJson("abc", SnapshotPartitioner.BytesMode, 1, null);
        SnapshotPartitioner.ShouldTranslateStoredData(header).Should().BeFalse();

        var pointer = """
            {"__externalSnapshotPayloadPointer":{"kind":"abs","blob":"runs/1/domain.json","bytes":10}}
            """;
        SnapshotPartitioner.ShouldTranslateStoredData(pointer).Should().BeTrue();

        var large = JsonSerializer.Serialize(new string('q', SnapshotPartitioner.MaxDocumentJsonBytes));
        SnapshotPartitioner.ShouldTranslateStoredData(large).Should().BeTrue();
    }

    [Fact]
    public void Escaped_size_matches_a_json_string_literal()
    {
        foreach (var value in new[] { "plain", "a\"b\\c", "line\n", "é😀", "\u0001", "<&>'+`", "\u007F" })
        {
            var literal = JsonSerializer.Serialize(value);
            var expected = Encoding.UTF8.GetByteCount(literal);
            (2 + SnapshotPartitioner.EscapedContentBytes(value)).Should().Be(expected);
        }
    }

    [Fact]
    public async Task Throttle_retries_with_backoff_then_succeeds_and_stops_on_other_errors()
    {
        var previous = CosmosThrottle.DelayOverride;
        CosmosThrottle.DelayOverride = TimeSpan.Zero;
        try
        {
            var calls = 0;
            await CosmosThrottle.ExecuteAsync(_ =>
            {
                calls++;
                if (calls < 3)
                    throw new InvalidOperationException("TooManyRequests");
                return Task.CompletedTask;
            }, CancellationToken.None);
            calls.Should().Be(3);

            var act = () => CosmosThrottle.ExecuteAsync(
                _ => throw new InvalidOperationException("not a throttle"),
                CancellationToken.None);
            await act.Should().ThrowAsync<InvalidOperationException>();

            var attempts = 0;
            var giveUp = () => CosmosThrottle.ExecuteAsync(_ =>
            {
                attempts++;
                throw new InvalidOperationException("code 16500");
            }, CancellationToken.None);
            await giveUp.Should().ThrowAsync<InvalidOperationException>();
            attempts.Should().Be(CosmosThrottle.MaxAttempts);
        }
        finally
        {
            CosmosThrottle.DelayOverride = previous;
        }
    }

    [Fact]
    public void Log_chunks_are_capped_under_the_cosmos_document_limit()
    {
        MongoSnapshotStore.MaxLogChunkEstimatedBsonBytes.Should().BeLessThanOrEqualTo(SnapshotPartitioner.MaxDocumentJsonBytes);
        MongoSnapshotStore.MaxLogChunkEstimatedBsonBytes.Should().BeLessThan(SnapshotPartitioner.HardCapBytes);
    }

    [Fact]
    public void Api_health_bodies_over_the_budget_are_cut_on_a_character_boundary()
    {
        ApiHealthResultBudget.Bound("short").Should().Be("short");
        var body = new string('q', ApiHealthResultBudget.MaxBodyBytes) + "😀tail";
        var bounded = ApiHealthResultBudget.Bound(body);
        Encoding.UTF8.GetByteCount(bounded!).Should().BeLessThanOrEqualTo(ApiHealthResultBudget.MaxBodyBytes);
        bounded.Should().EndWith(" [truncated: exceeded document budget]");
        bounded.Should().NotContain("\uFFFD");
    }

    private static string RoundTrip(SnapshotPlan.Partitioned split)
    {
        var header = SnapshotPartitioner.BuildHeaderJson("gen", split.Mode, split.Pieces.Count, split.SkeletonJson);
        SnapshotPartitioner.EstimateStoredDocumentBytes(header).Should().BeLessThanOrEqualTo(SnapshotPartitioner.HardCapBytes);
        return SnapshotPartitioner.Reassemble(split.Mode, split.SkeletonJson, split.Pieces);
    }

    private static void AssertEveryPieceFits(SnapshotPlan.Partitioned split, int budget)
    {
        split.Pieces.Should().NotBeEmpty();
        foreach (var piece in split.Pieces)
        {
            var estimate = SnapshotPartitioner.EstimateStoredDocumentBytes(piece.Data);
            estimate.Should().BeLessThanOrEqualTo(budget);
            estimate.Should().BeLessThanOrEqualTo(SnapshotPartitioner.HardCapBytes);
        }
    }

    private sealed record Record(int Id, string Note);

    private sealed record Wrapped(string Name, List<Record> Items);

    private sealed record Manifest(int PatientCount, Dictionary<string, Dictionary<string, int>> ResourceCountsByPatient);

    private sealed record NoteHolder(string Note);
}
