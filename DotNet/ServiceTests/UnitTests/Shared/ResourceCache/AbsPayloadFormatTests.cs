using FluentAssertions;
using LantanaGroup.Link.Shared.Application.Services.ResourceCache;
using System.Text;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.Shared.ResourceCache;

/// <summary>
/// Each append block lands atomically, so the guarantee that a failed or retried append never leaves a
/// torn line rests entirely on blocks being cut at record boundaries.
/// </summary>
[Trait("Category", "UnitTests")]
public class AbsPayloadFormatTests
{
    [Fact]
    public void BuildBlocks_RecordsUnderTarget_PacksIntoOneBlock()
    {
        var records = Records(3, jsonLength: 10);

        var blocks = AbsPayloadFormat.BuildBlocks(records, targetBlockBytes: 1024, maxBlockBytes: 4096);

        blocks.Should().ContainSingle();
        Decode(blocks).Should().Be(string.Concat(records.Select(record => record.Record)));
    }

    [Fact]
    public void BuildBlocks_BatchOverTarget_SplitsOnlyAtRecordBoundaries()
    {
        var records = Records(50, jsonLength: 90);

        var blocks = AbsPayloadFormat.BuildBlocks(records, targetBlockBytes: 1000, maxBlockBytes: 4096);

        blocks.Should().HaveCountGreaterThan(1);
        blocks.Should().OnlyContain(block => block.Length <= 1000);

        // Every block must end exactly at a pair boundary: a whole number of lines, an even number of them.
        foreach (var text in blocks.Select(block => Encoding.UTF8.GetString(block)))
        {
            text.Should().EndWith("\n");
            (text.Count(character => character == '\n') % 2).Should().Be(0);
        }

        Decode(blocks).Should().Be(string.Concat(records.Select(record => record.Record)));
    }

    [Fact]
    public void BuildBlocks_RecordOverTarget_GetsItsOwnBlock()
    {
        var small = Record("Observation/small", 10);
        var large = Record("Observation/large", 2000);
        var after = Record("Observation/after", 10);

        var blocks = AbsPayloadFormat.BuildBlocks([small, large, after], targetBlockBytes: 500, maxBlockBytes: 4096);

        blocks.Select(block => Encoding.UTF8.GetString(block))
            .Should().Equal(small.Record, large.Record, after.Record);
    }

    [Fact]
    public void BuildBlocks_RecordOverHardCap_Throws()
    {
        var oversized = Record("Binary/huge", 5000);

        var build = () => AbsPayloadFormat.BuildBlocks([oversized], targetBlockBytes: 500, maxBlockBytes: 4096);

        build.Should().Throw<InvalidOperationException>().WithMessage("*Binary/huge*");
    }

    [Fact]
    public void BuildBlocks_NoRecords_ReturnsNoBlocks()
    {
        AbsPayloadFormat.BuildBlocks([], targetBlockBytes: 500, maxBlockBytes: 4096).Should().BeEmpty();
    }

    [Fact]
    public async Task ReadPairsAsync_FirstBlockThenWholeBatchRetry_YieldsEveryReferenceOnce()
    {
        // The H1 failure: the first block of a batch lands, the append fails, and the retry appends the
        // whole batch again. Because blocks end on pair boundaries the repeat is aligned, so the reader
        // collapses it instead of pairing JSON lines with references.
        var records = Records(40, jsonLength: 90);
        var blocks = AbsPayloadFormat.BuildBlocks(records, targetBlockBytes: 1000, maxBlockBytes: 4096);
        blocks.Should().HaveCountGreaterThan(1);

        var blob = Decode([blocks[0]]) + Decode(blocks);

        var read = new List<(string Reference, string Json)>();
        var duplicates = await AbsPayloadFormat.ReadPairsAsync(
            new StringReader(blob),
            (reference, json) => read.Add((reference, json)));

        read.Select(pair => pair.Reference)
            .Should().Equal(records.Select(record => record.Reference));
        read.Should().OnlyContain(pair => pair.Json.StartsWith('{'));
        duplicates.Should().Be(Decode([blocks[0]]).Count(character => character == '\n') / 2);
    }

    private static List<(string Reference, string Record)> Records(int count, int jsonLength) =>
        Enumerable.Range(0, count)
            .Select(index => Record($"Observation/obs-{index}", jsonLength))
            .ToList();

    private static (string Reference, string Record) Record(string reference, int jsonLength)
    {
        var json = "{\"id\":\"" + new string('x', Math.Max(0, jsonLength - 10)) + "\"}";
        return (reference, AbsPayloadFormat.PayloadRecord(reference, json));
    }

    private static string Decode(IEnumerable<byte[]> blocks) =>
        string.Concat(blocks.Select(block => Encoding.UTF8.GetString(block)));
}
