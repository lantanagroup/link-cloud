using System.Text;
using System.Text.Json;
using Automation.UI.Models;

namespace Automation.UI.Services;

/// <summary>
/// Validates a FHIR measure transaction bundle and extracts display metadata.
/// </summary>
public static class MeasureBundleParser
{
    public const int MaxBundleBytes = 12 * 1024 * 1024;

    public sealed record ParseResult(
        string MeasureId,
        string? CanonicalUrl,
        string? Version,
        string? MeasureDate,
        string? Status,
        string? Title);

    public static ParseResult Parse(string bundleJson)
    {
        if (string.IsNullOrWhiteSpace(bundleJson))
            throw new InvalidOperationException("Measure bundle JSON is required.");

        if (Encoding.UTF8.GetByteCount(bundleJson) > MaxBundleBytes)
            throw new InvalidOperationException($"Measure bundle exceeds {MaxBundleBytes / (1024 * 1024)} MB.");

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(bundleJson);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"Measure bundle is not valid JSON: {ex.Message}");
        }

        using (doc)
        {
            var root = doc.RootElement;
            var resourceType = root.TryGetProperty("resourceType", out var rt) ? rt.GetString() : null;
            if (!string.Equals(resourceType, "Bundle", StringComparison.Ordinal))
                throw new InvalidOperationException("Measure file must be a FHIR Bundle.");

            if (!root.TryGetProperty("entry", out var entries) || entries.ValueKind != JsonValueKind.Array)
                throw new InvalidOperationException("Measure bundle has no entries.");

            JsonElement? measure = null;
            var hasLibrary = false;

            foreach (var entry in entries.EnumerateArray())
            {
                if (!entry.TryGetProperty("resource", out var resource))
                    continue;

                var type = resource.TryGetProperty("resourceType", out var t) ? t.GetString() : null;
                if (string.Equals(type, "Measure", StringComparison.Ordinal) && measure == null)
                    measure = resource;
                else if (string.Equals(type, "Library", StringComparison.Ordinal))
                    hasLibrary = true;
            }

            if (measure == null)
                throw new InvalidOperationException("Measure bundle must contain a Measure resource.");
            if (!hasLibrary)
                throw new InvalidOperationException("Measure bundle must contain a Library resource.");

            var measureId = measure.Value.TryGetProperty("id", out var idProp) ? idProp.GetString() : null;
            if (string.IsNullOrWhiteSpace(measureId))
                throw new InvalidOperationException("Measure resource must have an id.");

            return new ParseResult(
                MeasureId: measureId,
                CanonicalUrl: GetString(measure.Value, "url"),
                Version: GetString(measure.Value, "version"),
                MeasureDate: GetString(measure.Value, "date"),
                Status: GetString(measure.Value, "status"),
                Title: GetString(measure.Value, "title") ?? GetString(measure.Value, "name"));
        }
    }

    public static void ApplyMetadata(MeasureTemplate template, ParseResult parsed)
    {
        template.MeasureId = parsed.MeasureId;
        template.CanonicalUrl = parsed.CanonicalUrl;
        template.Version = parsed.Version;
        template.MeasureDate = parsed.MeasureDate;
        template.Status = parsed.Status;
        if (string.IsNullOrWhiteSpace(template.Name) && !string.IsNullOrWhiteSpace(parsed.Title))
            template.Name = parsed.Title;
    }

    private static string? GetString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var prop) && prop.ValueKind == JsonValueKind.String
            ? prop.GetString()
            : null;
}
