using System.Reflection;
using System.Text.Json;

namespace LantanaGroup.Link.Shared.Application.Models.Kafka;

/// <summary>
/// Reads identifiers from the legacy key formats used before identifiers moved onto the value.
/// TODO: remove this legacy key fallback after one release, once every producer writes the identifiers on the value.
/// </summary>
public static class KafkaKeyLegacy
{
    // Plain keys produced for a service (health checks, audits with no facility) are not facility ids.
    // The names live in one fixture so the .NET and Java readers stay the same list.
    private static readonly HashSet<string> ServiceNames = LoadServiceNames();

    private static HashSet<string> LoadServiceNames()
    {
        const string resource = "LantanaGroup.Link.Shared.kafka-service-names.json";
        using var stream = typeof(KafkaKeyLegacy).Assembly.GetManifestResourceStream(resource)
            ?? throw new InvalidOperationException("Service name list is missing.");
        var names = JsonSerializer.Deserialize<string[]>(stream)
            ?? throw new InvalidOperationException("Service name list is empty.");
        if (names.Length == 0 || names.Any(name => string.IsNullOrEmpty(name)))
        {
            throw new InvalidOperationException("Service name list is empty.");
        }

        if (names.Distinct(StringComparer.Ordinal).Count() != names.Length)
        {
            throw new InvalidOperationException("Service name list contains a duplicate.");
        }

        return new HashSet<string>(names, StringComparer.Ordinal);
    }

    public static bool TryReadFacility(string? key, out string facilityId)
    {
        facilityId = string.Empty;
        if (!TryOpen(key, out var document, out var plain))
        {
            return false;
        }

        if (document != null)
        {
            using (document)
            {
                if (TryProperty(document.RootElement, "facilityId", out var value) && !string.IsNullOrWhiteSpace(value))
                {
                    facilityId = value;
                    return true;
                }
            }

            return false;
        }

        if (ServiceNames.Contains(plain!))
        {
            return false;
        }

        facilityId = plain!;
        return true;
    }

    public static bool TryReadPatient(string? key, out string patientId)
    {
        patientId = string.Empty;
        if (!TryOpen(key, out var document, out _))
        {
            return false;
        }

        if (document == null)
        {
            return false;
        }

        using (document)
        {
            if (TryProperty(document.RootElement, "patientId", out var value) && !string.IsNullOrWhiteSpace(value))
            {
                patientId = value;
                return true;
            }
        }

        return false;
    }

    public static bool TryReadReportScheduleId(string? key, out Guid reportScheduleId)
    {
        reportScheduleId = Guid.Empty;
        if (!TryOpen(key, out var document, out _))
        {
            return false;
        }

        if (document == null)
        {
            return false;
        }

        using (document)
        {
            if (TryProperty(document.RootElement, "reportScheduleId", out var value)
                && Guid.TryParse(value, out reportScheduleId)
                && reportScheduleId != Guid.Empty)
            {
                return true;
            }
        }

        reportScheduleId = Guid.Empty;
        return false;
    }

    private static bool TryOpen(string? key, out JsonDocument? document, out string? plain)
    {
        document = null;
        plain = null;
        if (string.IsNullOrWhiteSpace(key))
        {
            return false;
        }

        var trimmed = key.Trim();
        if (trimmed.StartsWith('{'))
        {
            try
            {
                document = JsonDocument.Parse(trimmed);
                return document.RootElement.ValueKind == JsonValueKind.Object;
            }
            catch (JsonException)
            {
                return false;
            }
        }

        // A colon is the current partition key, which is not a data source.
        if (trimmed.Contains(':'))
        {
            return false;
        }

        plain = trimmed;
        return true;
    }

    private static bool TryProperty(JsonElement element, string name, out string value)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase)
                && property.Value.ValueKind == JsonValueKind.String)
            {
                value = property.Value.GetString() ?? string.Empty;
                return true;
            }
        }

        value = string.Empty;
        return false;
    }
}
