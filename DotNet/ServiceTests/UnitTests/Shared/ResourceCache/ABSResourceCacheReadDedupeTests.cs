using FluentAssertions;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using LantanaGroup.Link.Shared.Application.SerDes;
using LantanaGroup.Link.Shared.Application.Services.ResourceCache;
using System.Text.Json;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.Shared.ResourceCache;

/// <summary>
/// The payload blob can legitimately hold the same reference twice: a crash between the payload
/// append and the ids append leaves a resource the retry's diff will not skip, a retried append repeats
/// whole blocks, and two processes appending to one key can interleave. These cover the shape of what
/// the reader has to collapse.
/// </summary>
[Trait("Category", "UnitTests")]
public class ABSResourceCacheReadDedupeTests
{
    [Fact]
    public async Task ReadPairsAsync_RepeatedReference_YieldsOneResourcePerDistinctReference()
    {
        var json = new Patient { Id = "1" }.ToJson();

        // Exactly what an interrupted write leaves behind: reference/JSON line pairs, one repeated.
        var payload = string.Join("\n", ["Patient/1", json, "Patient/1", json, "Patient/2", json]);

        var kept = new List<string>();
        var duplicates = await AbsPayloadFormat.ReadPairsAsync(
            new StringReader(payload),
            (reference, body) =>
            {
                JsonSerializer.Deserialize<DomainResource>(body, LinkFhirSerializerOptions.ForFhirLenientSerialization)
                    .Should().NotBeNull();
                kept.Add(reference);
            });

        kept.Should().Equal("Patient/1", "Patient/2");
        duplicates.Should().Be(1);
    }

    [Fact]
    public async Task ReadPairsAsync_WindowsLineEndings_AreStillReadAsPairs()
    {
        // Blobs written before the block writer used Environment.NewLine, which is \r\n on Windows hosts.
        var json = new Patient { Id = "1" }.ToJson();
        var payload = "Patient/1\r\n" + json + "\r\n";

        var kept = new List<string>();
        await AbsPayloadFormat.ReadPairsAsync(new StringReader(payload), (reference, _) => kept.Add(reference));

        kept.Should().Equal("Patient/1");
    }
}
