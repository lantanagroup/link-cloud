using Hl7.Fhir.Model;
using LantanaGroup.Link.Shared.Application.SerDes;
using System.Text.Json;

namespace LantanaGroup.Link.Report.Services
{
    /// <summary>
    /// Writes FHIR resources as NDJSON, one resource per line. The manifest upload and the ad-hoc
    /// manifest endpoint both use it, so a downloaded manifest serializes exactly like an uploaded one.
    /// </summary>
    public static class FhirNdjsonWriter
    {
        public const string ContentType = "application/x-ndjson";

        private static readonly ReadOnlyMemory<byte> LineFeed = new([0x0a]);

        public static async System.Threading.Tasks.Task WriteAsync(Stream stream, IEnumerable<Resource> resources, CancellationToken cancellationToken = default)
        {
            foreach (var resource in resources)
            {
                await JsonSerializer.SerializeAsync(stream, resource, LinkFhirSerializerOptions.ForFhirLenientSerialization, cancellationToken);
                await stream.WriteAsync(LineFeed, cancellationToken);
            }
        }
    }
}
