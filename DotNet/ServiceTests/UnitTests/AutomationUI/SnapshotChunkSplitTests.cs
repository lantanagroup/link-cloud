using Automation.UI.Services.Persistence;
using FluentAssertions;

namespace UnitTests.AutomationUI;

[Trait("Category", "UnitTests")]
public class SnapshotChunkSplitTests
{
    [Fact]
    public void Split_keeps_a_payload_that_is_exactly_the_max()
    {
        var json = new string('a', 12);
        var slices = MongoSnapshotStore.SplitUtf8(json, 12);
        slices.Should().ContainSingle().Which.Should().Be(json);
    }

    [Fact]
    public void Split_puts_one_byte_over_the_max_in_the_next_slice()
    {
        var json = new string('a', 13);
        var slices = MongoSnapshotStore.SplitUtf8(json, 12);
        slices.Should().Equal(new string('a', 12), "a");
        string.Concat(slices).Should().Be(json);
    }

    [Fact]
    public void Split_does_not_break_a_multibyte_character()
    {
        var emoji = "😀";
        var json = new string('a', 10) + emoji;
        var slices = MongoSnapshotStore.SplitUtf8(json, 12);
        slices.Should().Equal(new string('a', 10), emoji);
        string.Concat(slices).Should().Be(json);
    }

    [Fact]
    public void Split_of_an_empty_payload_has_no_slices()
    {
        MongoSnapshotStore.SplitUtf8(string.Empty, MongoSnapshotStore.SnapshotChunkBytes).Should().BeEmpty();
    }
}
