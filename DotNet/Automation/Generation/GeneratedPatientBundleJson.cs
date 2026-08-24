using System.Text;
using System.Text.Json;

namespace LantanaGroup.Automation.Generation;

/// <summary>
/// Merges the per-patient transaction chunks the pipeline uploads into one
/// collection Bundle for download / inspection.
/// </summary>
public static class GeneratedPatientBundleJson
{
    public static string MergeToCollection(IReadOnlyList<string> uploadedBundleJson)
    {
        ArgumentNullException.ThrowIfNull(uploadedBundleJson);

        var entries = new List<JsonElement>();
        foreach (var json in uploadedBundleJson)
        {
            if (string.IsNullOrWhiteSpace(json))
                continue;

            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("entry", out var entryArray)
                || entryArray.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var entry in entryArray.EnumerateArray())
                entries.Add(entry.Clone());
        }

        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();
            writer.WriteString("resourceType", "Bundle");
            writer.WriteString("type", "collection");
            writer.WriteNumber("total", entries.Count);
            writer.WritePropertyName("entry");
            writer.WriteStartArray();
            foreach (var entry in entries)
                entry.WriteTo(writer);
            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }
}
