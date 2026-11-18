using System.Text.Json;
using Link.UI.Models;

namespace Link.UI.Services;

public static class RubricSample
{
    public static SampleReading Read(string? text)
    {
        var trimmed = text?.Trim() ?? "";
        if (trimmed.Length == 0)
            return new SampleReading { Error = "Provide a sample to run against." };
        if (trimmed.StartsWith('<'))
            return new SampleReading { Error = "The sample looks like XML. A dry run takes FHIR as JSON." };
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(trimmed);
        }
        catch (JsonException ex)
        {
            return new SampleReading { Error = "The sample is not valid JSON: " + ex.Message + "." };
        }

        using (document)
        {
            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty("resourceType", out var type)
                || type.ValueKind != JsonValueKind.String)
            {
                return new SampleReading { Error = "The sample must be one FHIR resource or a Bundle: a JSON object with a resourceType." };
            }

            var resourceType = type.GetString() ?? "";
            string description;
            if (resourceType == "Bundle")
            {
                var count = document.RootElement.TryGetProperty("entry", out var entry) && entry.ValueKind == JsonValueKind.Array
                    ? entry.GetArrayLength()
                    : 0;
                description = "Bundle with " + count + (count == 1 ? " entry" : " entries");
            }
            else
            {
                description = resourceType + " resource";
            }

            return new SampleReading
            {
                Sample = document.RootElement.Clone(),
                Description = description
            };
        }
    }
}
