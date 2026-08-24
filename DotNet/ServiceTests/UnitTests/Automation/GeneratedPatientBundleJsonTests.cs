using System.Text.Json;
using FluentAssertions;
using LantanaGroup.Automation.Generation;

namespace UnitTests.Automation;

[Trait("Category", "UnitTests")]
public class GeneratedPatientBundleJsonTests
{
    [Fact]
    public void MergeToCollection_combines_transaction_chunk_entries()
    {
        var chunk1 = """{"resourceType":"Bundle","type":"transaction","entry":[{"resource":{"resourceType":"Patient","id":"p1"}}]}""";
        var chunk2 = """{"resourceType":"Bundle","type":"transaction","entry":[{"resource":{"resourceType":"Encounter","id":"e1"}},{"resource":{"resourceType":"Observation","id":"o1"}}]}""";

        var merged = GeneratedPatientBundleJson.MergeToCollection([chunk1, chunk2, " ", ""]);

        using var doc = JsonDocument.Parse(merged);
        var root = doc.RootElement;
        root.GetProperty("resourceType").GetString().Should().Be("Bundle");
        root.GetProperty("type").GetString().Should().Be("collection");
        root.GetProperty("total").GetInt32().Should().Be(3);

        var ids = root.GetProperty("entry").EnumerateArray()
            .Select(e => e.GetProperty("resource").GetProperty("id").GetString())
            .ToList();
        ids.Should().Equal("p1", "e1", "o1");
    }

    [Fact]
    public void MergeToCollection_skips_json_without_entry_array()
    {
        var merged = GeneratedPatientBundleJson.MergeToCollection(
        [
            """{"resourceType":"Patient","id":"p1"}""",
            """{"resourceType":"Bundle","type":"transaction"}"""
        ]);

        using var doc = JsonDocument.Parse(merged);
        doc.RootElement.GetProperty("total").GetInt32().Should().Be(0);
        doc.RootElement.GetProperty("entry").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public void MergeToCollection_empty_list_is_empty_collection()
    {
        var merged = GeneratedPatientBundleJson.MergeToCollection([]);

        using var doc = JsonDocument.Parse(merged);
        doc.RootElement.GetProperty("resourceType").GetString().Should().Be("Bundle");
        doc.RootElement.GetProperty("type").GetString().Should().Be("collection");
        doc.RootElement.GetProperty("total").GetInt32().Should().Be(0);
    }
}
