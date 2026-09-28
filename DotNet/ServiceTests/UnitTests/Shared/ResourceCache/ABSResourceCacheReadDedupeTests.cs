using FluentAssertions;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using LantanaGroup.Link.Shared.Application.SerDes;
using System.Text.Json;

namespace UnitTests.Shared.ResourceCache;

/// <summary>
/// The payload blob can legitimately hold the same reference twice: a crash between the payload
/// append and the ids append leaves a resource the retry's diff will not skip, and two processes
/// appending to one key can interleave. These cover the shape of what the reader has to collapse.
/// </summary>
[Trait("Category", "UnitTests")]
public class ABSResourceCacheReadDedupeTests
{
    [Fact]
    public void A_payload_with_a_repeated_reference_yields_one_resource_per_distinct_reference()
    {
        var patient = new Patient { Id = "1" };
        var json = patient.ToJson();

        // Exactly what an interrupted write leaves behind: reference/JSON line pairs, one repeated.
        var payload = string.Join("\n", ["Patient/1", json, "Patient/1", json, "Patient/2", json]);

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var kept = new List<string>();

        using var reader = new StringReader(payload);
        while (true)
        {
            var reference = reader.ReadLine();
            if (reference == null)
            {
                break;
            }

            var body = reader.ReadLine();
            if (body == null)
            {
                break;
            }

            if (!seen.Add(reference))
            {
                continue;
            }

            JsonSerializer.Deserialize<DomainResource>(body, LinkFhirSerializerOptions.ForFhirLenientSerialization)
                .Should().NotBeNull();
            kept.Add(reference);
        }

        kept.Should().Equal("Patient/1", "Patient/2");
    }
}
