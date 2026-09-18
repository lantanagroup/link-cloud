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
        root.TryGetProperty("total", out _).Should().BeFalse();

        var ids = root.GetProperty("entry").EnumerateArray()
            .Select(e => e.GetProperty("resource").GetProperty("id").GetString())
            .ToList();
        ids.Should().Equal("p1", "e1", "o1");
    }

    [Fact]
    public void MergeToCollection_strips_transaction_request_from_entries()
    {
        var chunk = """
            {"resourceType":"Bundle","type":"transaction","entry":[{
              "fullUrl":"urn:uuid:p1",
              "resource":{"resourceType":"Patient","id":"p1"},
              "request":{"method":"PUT","url":"Patient/p1"}
            }]}
            """;

        var merged = GeneratedPatientBundleJson.MergeToCollection([chunk]);
        using var doc = JsonDocument.Parse(merged);
        var entry = doc.RootElement.GetProperty("entry")[0];
        entry.TryGetProperty("request", out _).Should().BeFalse();
        entry.GetProperty("fullUrl").GetString().Should().Be("urn:uuid:p1");
        entry.GetProperty("resource").GetProperty("id").GetString().Should().Be("p1");
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
        doc.RootElement.GetProperty("entry").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public void MergeToCollection_empty_list_is_empty_collection()
    {
        var merged = GeneratedPatientBundleJson.MergeToCollection([]);

        using var doc = JsonDocument.Parse(merged);
        doc.RootElement.GetProperty("resourceType").GetString().Should().Be("Bundle");
        doc.RootElement.GetProperty("type").GetString().Should().Be("collection");
        doc.RootElement.GetProperty("entry").GetArrayLength().Should().Be(0);
    }
}
